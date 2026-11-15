using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RiskInference.Api.Contracts;
using RiskInference.Api.Model;
using RiskInference.Api.Operations;

namespace RiskInference.Api.Tests;

public sealed record CapturedLog(LogLevel Level, string Category, string Message, Dictionary<string, object?> State, Exception? Exception);

// Keeps every log line, with its structured values, so a test can check them.
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLog> Logs { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Logs);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = new Dictionary<string, object?>();
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                foreach (var pair in pairs) values[pair.Key] = pair.Value;
            sink.Enqueue(new CapturedLog(logLevel, category, formatter(state, exception), values, exception));
        }
    }
}

// The real application on a real port, with captured logs.
internal sealed class OperationsTestHost : IAsyncDisposable
{
    public WebApplication App { get; }
    public HttpClient Client { get; }
    public CapturingLoggerProvider Capture { get; } = new();
    private bool _stopped;

    public const string ValidBody = """
        { "amount": 4500, "transactionHour": 2, "customerTransactionCount24h": 12,
          "recentFailureCount": 3, "beneficiaryAgeDays": 2, "isHighRiskCountry": true }
        """;

    private OperationsTestHost(WebApplication app, CapturingLoggerProvider capture)
    {
        App = app;
        Capture = capture;
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(30) };
    }

    // manifestPath: null means the shipped manifest.
    public static async Task<OperationsTestHost> StartAsync(
        string? manifestPath = null,
        Action<WebApplicationBuilder>? configure = null,
        int integrityCheckSeconds = 3600,
        int shutdownSeconds = 20)
    {
        var capture = new CapturingLoggerProvider();
        var app = RiskApiHost.Build(
            [
                "--urls", "http://127.0.0.1:0",
                $"--RiskModel:ManifestPath={manifestPath ?? TestSupport.ManifestPath}",
                $"--RiskModel:IntegrityCheckIntervalSeconds={integrityCheckSeconds}",
                $"--RiskModel:ShutdownTimeoutSeconds={shutdownSeconds}"
            ],
            builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.SetMinimumLevel(LogLevel.Debug);
                builder.Logging.AddProvider(capture);
                configure?.Invoke(builder);
            });
        await app.StartAsync();
        return new OperationsTestHost(app, capture);
    }

    public IModelHealth Health => App.Services.GetRequiredService<IModelHealth>();
    public RiskMetrics Metrics => App.Services.GetRequiredService<RiskMetrics>();
    public IReadOnlyList<CapturedLog> Logs => Capture.Logs.ToList();

    public Task<HttpResponseMessage> PostAsync(string body, string? requestId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/risk/predict")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (requestId is not null) request.Headers.Add("X-Request-ID", requestId);
        return Client.SendAsync(request);
    }

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    public async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        await App.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        Client.Dispose();
        await App.DisposeAsync();
    }
}
