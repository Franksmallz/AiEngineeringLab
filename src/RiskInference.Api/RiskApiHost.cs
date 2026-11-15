using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Endpoints;
using RiskInference.Api.Model;

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

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
        builder.Services.AddSingleton<IPathResolver, PathResolver>();
        builder.Services.AddSingleton<IRiskModelService, RiskModelService>();

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

        // An unexpected error returns a generic 500 body. It never shows a stack trace.
        app.UseExceptionHandler();

        app.MapRiskEndpoints();
        return app;
    }
}
