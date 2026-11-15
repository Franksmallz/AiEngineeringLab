using RiskInference.Api.Contracts;
using RiskInference.Api.Model;

namespace RiskInference.Api.Endpoints;

// Thin HTTP layer. It validates the request and calls the inference service.
// Feature engineering, scoring and the threshold decision stay in the service.
public static class RiskEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/risk/predict", (RiskPredictionRequest request, IRiskModelService model) =>
        {
            var errors = request.Validate();
            if (errors.Count > 0)
                return Results.ValidationProblem(errors.ToDictionary(e => e.Key, e => e.Value));

            return Results.Ok(model.Predict(request.ToModelInput()));
        })
        .Produces<RiskPredictionResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        // The model loads in the constructor of the singleton. The app creates
        // it before it listens, so a reachable app has a loaded model. The body
        // names the versions in use, so an operator can see what is deployed.
        app.MapGet("/health", (IRiskModelService model) => Results.Ok(new
        {
            status = "Healthy",
            modelVersion = model.Active.ModelVersion,
            decisionPolicyVersion = model.Active.DecisionPolicyVersion,
            featureSchemaVersion = model.Active.FeatureSchemaVersion,
            threshold = model.Active.Threshold
        }));

        return app;
    }
}
