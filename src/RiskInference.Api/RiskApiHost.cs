using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Endpoints;
using RiskInference.Api.Model;
using RiskInference.Api.Operations;

namespace RiskInference.Api;

// Builds the web application. Program.cs and the tests both call this, so the
// tests start the same application that production runs.
public static class RiskApiHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bad configuration stops the start-up with a clear error.
        builder.Services
            .AddOptions<RiskModelOptions>()
            .BindConfiguration(RiskModelOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // On stop, the host stops accepting new connections and waits for the
        // requests in progress, up to this time. After that it ends them.
        var shutdownSeconds = builder.Configuration
            .GetValue<int?>($"{RiskModelOptions.SectionName}:{nameof(RiskModelOptions.ShutdownTimeoutSeconds)}")
            ?? new RiskModelOptions().ShutdownTimeoutSeconds;
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(shutdownSeconds));

        // An unreadable request body must reach our exception handler in every environment.
        // By default only the Development environment throws. In Production the framework
        // would answer with an empty 400 and skip the request ID, the log outcome and the metric.
        builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

        builder.Services.AddProblemDetails();
        // The handlers run in this order. The last one takes every other error.
        builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
        builder.Services.AddExceptionHandler<UnexpectedErrorHandler>();

        builder.Services.AddSingleton<IPathResolver, PathResolver>();
        builder.Services.AddSingleton<IRiskModelService, RiskModelService>();
        builder.Services.AddSingleton<RiskMetrics>();
        builder.Services.AddSingleton<ModelHealthMonitor>();
        builder.Services.AddSingleton<IModelHealth>(sp => sp.GetRequiredService<ModelHealthMonitor>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ModelHealthMonitor>());

        configure?.Invoke(builder);

        var app = builder.Build();

        // Create the model service now, before the app accepts a request. A bad
        // manifest, or a missing, changed, corrupted or incompatible model,
        // throws here and stops the start-up.
        var model = app.Services.GetRequiredService<IRiskModelService>();
        app.Logger.LogInformation(
            "Risk model loaded. Model {ModelVersion}, policy {PolicyVersion}, features {FeatureSchemaVersion}, " +
            "threshold {Threshold}, sha256 {Sha256}.",
            model.Active.ModelVersion, model.Active.DecisionPolicyVersion, model.Active.FeatureSchemaVersion,
            model.Active.Threshold, model.Active.ModelSha256);

        // Order matters. The observability middleware is first, so it sees the final
        // status of a request that the exception handler turned into a 500.
        app.UseMiddleware<RequestObservabilityMiddleware>();
        app.UseExceptionHandler();

        app.MapRiskEndpoints();
        app.MapOperationsEndpoints();
        return app;
    }
}
