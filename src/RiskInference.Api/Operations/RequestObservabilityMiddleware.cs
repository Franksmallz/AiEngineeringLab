using System.Diagnostics;
using System.Text.RegularExpressions;
using RiskInference.Api.Model;

namespace RiskInference.Api.Operations;

// The handlers tell the middleware how a request ended, through HttpContext.Items.
public static class RequestInfo
{
    private const string OutcomeKey = "risk.outcome";
    private const string PredictedKey = "risk.predicted";
    private const string FlaggedKey = "risk.flagged";

    public static void SetOutcome(this HttpContext context, RequestOutcome outcome) => context.Items[OutcomeKey] = outcome;
    public static RequestOutcome? GetOutcome(this HttpContext context) =>
        context.Items.TryGetValue(OutcomeKey, out var v) ? (RequestOutcome)v! : null;

    public static void SetPrediction(this HttpContext context, string predictedRisk, bool flagged)
    {
        context.Items[PredictedKey] = predictedRisk;
        context.Items[FlaggedKey] = flagged;
    }

    public static string? GetPredictedRisk(this HttpContext context) =>
        context.Items.TryGetValue(PredictedKey, out var v) ? (string?)v : null;

    public static bool GetFlagged(this HttpContext context) =>
        context.Items.TryGetValue(FlaggedKey, out var v) && (bool)v!;
}

// For every request: give it a request ID, measure its duration, count it, and
// write one structured log line when it ends.
//
// The log line has: request ID, model version, policy version, duration, outcome,
// HTTP status, route, and (for a prediction) the predicted risk level and the flag.
// It has no transaction field and no customer data. The request body is never read here.
//
// Put this middleware before the exception handler, so it sees the final status
// code of a request that failed.
public sealed partial class RequestObservabilityMiddleware
{
    public const string RequestIdHeader = "X-Request-ID";
    private static readonly Regex SafeRequestId = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestObservabilityMiddleware> _logger;

    public RequestObservabilityMiddleware(RequestDelegate next, ILogger<RequestObservabilityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRiskModelService model, RiskMetrics metrics)
    {
        // Use the ID of the caller if it is safe to log. Else make a new one.
        var requestId = context.Request.Headers[RequestIdHeader].FirstOrDefault();
        if (requestId is null || !SafeRequestId.IsMatch(requestId))
            requestId = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = requestId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[RequestIdHeader] = requestId;
            return Task.CompletedTask;
        });

        var started = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            var durationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var isPrediction = context.Request.Path == "/risk/predict";
            var status = context.Response.StatusCode;

            // The handlers set the outcome. A 5xx without an outcome is an unexpected error.
            // Other requests (for example 404 or 405) are not prediction requests and are not counted.
            var outcome = context.GetOutcome() ?? (status >= 500 ? RequestOutcome.Error : null);
            var predicted = context.GetPredictedRisk();
            var flagged = context.GetFlagged();

            if (isPrediction && outcome is not null)
                metrics.Record(outcome.Value, durationMs, predicted, flagged);

            // Only the route pattern is logged, never the raw path or the query string.
            // The exception handler clears the endpoint, so a malformed prediction request needs the fixed name.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                        ?? (isPrediction ? "/risk/predict" : "unmatched");
            var level = isPrediction ? (status >= 500 ? LogLevel.Error : LogLevel.Information) : LogLevel.Debug;
            if (_logger.IsEnabled(level))
                LogCompleted(level, context.Request.Method, route, status,
                    outcome?.ToString() ?? "none", Math.Round(durationMs, 3),
                    model.Active.ModelVersion, model.Active.DecisionPolicyVersion, requestId, predicted, predicted is null ? null : flagged);
        }
    }

    [LoggerMessage(EventId = 100, Message =
        "Request completed. Method {Method} Route {Route} Status {StatusCode} Outcome {Outcome} DurationMs {DurationMs} " +
        "ModelVersion {ModelVersion} PolicyVersion {PolicyVersion} RequestId {RequestId} PredictedRisk {PredictedRisk} Flagged {Flagged}")]
    private partial void LogCompleted(LogLevel level, string method, string route, int statusCode, string outcome,
        double durationMs, string modelVersion, string policyVersion, string requestId, string? predictedRisk, bool? flagged);
}
