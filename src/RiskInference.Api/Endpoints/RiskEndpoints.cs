using RiskInference.Api.Contracts;
using RiskInference.Api.Model;
using RiskInference.Api.Operations;

namespace RiskInference.Api.Endpoints;

// Thin HTTP layer. It validates the request and calls the inference service.
// Feature engineering, scoring and the threshold decision stay in the service.
public static class RiskEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/risk/predict", (RiskPredictionRequest request, IRiskModelService model, IModelHealth health, HttpContext http) =>
        {
            // Do not serve predictions from a model that failed its integrity check.
            // A host that is stopping is not ready, but it still answers the requests that
            // reach it until the shutdown period ends. A load balancer needs a moment to notice.
            var readiness = health.Current;
            if (!readiness.Ready && readiness.Reason != ReadinessStatus.ShuttingDown)
            {
                http.SetOutcome(RequestOutcome.NotReady);
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The service is not ready to serve predictions.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["requestId"] = http.TraceIdentifier,
                        ["reason"] = readiness.Reason
                    });
            }

            var errors = request.Validate();
            if (errors.Count > 0)
            {
                http.SetOutcome(RequestOutcome.ValidationFailed);
                return Results.ValidationProblem(
                    errors.ToDictionary(e => e.Key, e => e.Value),
                    extensions: new Dictionary<string, object?> { ["requestId"] = http.TraceIdentifier });
            }

            var response = model.Predict(request.ToModelInput());
            http.SetOutcome(RequestOutcome.Predicted);
            http.SetPrediction(response.PredictedRisk, response.FlaggedHighRisk);
            return Results.Ok(response);
        })
        .Produces<RiskPredictionResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
