using RiskInference.Api.Model;

namespace RiskInference.Api.Operations;

// The readiness of the service. Reason is a short fixed code. It never holds a
// path, a hash or an exception text, so it is safe to return from an endpoint.
public sealed record ReadinessStatus(bool Ready, string Reason, DateTimeOffset CheckedAt)
{
    public const string Ok = "ok";
    public const string ShuttingDown = "shutting_down";
}

public interface IModelHealth
{
    // The result of the last check. Reading it is cheap. It is safe to call on every request.
    ReadinessStatus Current { get; }

    // Runs a check now and returns the new result.
    Task<ReadinessStatus> CheckNowAsync(CancellationToken cancellationToken = default);
}

// Liveness: the process runs. Readiness: the service can safely serve predictions.
//
// The model is loaded and checked at start-up, so a service with a bad model
// never starts. This monitor covers what happens after that: it repeats the
// integrity check at an interval. If the manifest or the model file on disk is
// missing, changed or corrupted, or if the model gives a different answer for
// the fixed test input, then the service is no longer ready. It becomes ready
// again when the check passes. When the host begins to stop, it is no longer ready.
public sealed class ModelHealthMonitor : BackgroundService, IModelHealth
{
    private readonly IRiskModelService _model;
    private readonly TimeSpan _interval;
    private readonly ILogger<ModelHealthMonitor> _logger;
    private readonly SemaphoreSlim _oneCheckAtATime = new(1, 1);
    private volatile ReadinessStatus _current = new(true, ReadinessStatus.Ok, DateTimeOffset.UtcNow);
    private volatile bool _stopping;

    public ModelHealthMonitor(
        IRiskModelService model,
        Microsoft.Extensions.Options.IOptions<RiskModelOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<ModelHealthMonitor> logger)
    {
        _model = model;
        _interval = TimeSpan.FromSeconds(options.Value.IntegrityCheckIntervalSeconds);
        _logger = logger;
        lifetime.ApplicationStopping.Register(() => _stopping = true);
    }

    public ReadinessStatus Current =>
        _stopping ? new ReadinessStatus(false, ReadinessStatus.ShuttingDown, DateTimeOffset.UtcNow) : _current;

    public async Task<ReadinessStatus> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        await _oneCheckAtATime.WaitAsync(cancellationToken);
        try
        {
            // The check reads files and runs a prediction. Do it off the request threads.
            var problem = await Task.Run(_model.VerifyIntegrity, cancellationToken);
            var next = new ReadinessStatus(problem is null, problem ?? ReadinessStatus.Ok, DateTimeOffset.UtcNow);

            var previous = _current;
            _current = next;
            if (previous.Ready && !next.Ready)
                _logger.LogError("Readiness lost. Reason {Reason}. ModelVersion {ModelVersion}.",
                    next.Reason, _model.Active.ModelVersion);
            else if (!previous.Ready && next.Ready)
                _logger.LogWarning("Readiness restored. ModelVersion {ModelVersion}.", _model.Active.ModelVersion);
            return next;
        }
        finally { _oneCheckAtATime.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await CheckNowAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { _logger.LogError(ex, "The integrity check itself failed."); }
            }
        }
        catch (OperationCanceledException) { }
    }
}
