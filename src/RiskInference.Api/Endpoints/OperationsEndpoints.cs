using RiskInference.Api.Model;
using RiskInference.Api.Operations;

namespace RiskInference.Api.Endpoints;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness: is the process working? It checks nothing else. If it fails, restart the process.
        // A bad model must not make this fail: a restart would not repair a bad model file.
        app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }));

        // Readiness: can the service safely serve predictions now? If it fails, send no traffic to it.
        // It fails (503) when the model or the manifest fails the integrity check, and when the host is stopping.
        app.MapGet("/health/ready", (IRiskModelService model, IModelHealth health) =>
        {
            var readiness = health.Current;
            if (!readiness.Ready)
                return Results.Json(new
                {
                    status = "NotReady",
                    reason = readiness.Reason,
                    checkedAt = readiness.CheckedAt
                }, statusCode: StatusCodes.Status503ServiceUnavailable);

            return Results.Ok(new
            {
                status = "Ready",
                modelVersion = model.Active.ModelVersion,
                decisionPolicyVersion = model.Active.DecisionPolicyVersion,
                featureSchemaVersion = model.Active.FeatureSchemaVersion,
                threshold = model.Active.Threshold,
                checkedAt = readiness.CheckedAt
            });
        });

        // Aggregate numbers only: counts, rates and times. No transaction data.
        // Do not expose this endpoint to the public network.
        app.MapGet("/metrics", (RiskMetrics metrics, IRiskModelService model) =>
            Results.Ok(metrics.Snapshot(model.Active.ModelVersion, model.Active.DecisionPolicyVersion)));

        return app;
    }
}
