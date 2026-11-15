using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Endpoints;
using RiskInference.Api.Model;

var builder = WebApplication.CreateBuilder(args);

// Bad configuration stops the start-up with a clear error.
builder.Services
    .AddOptions<RiskModelOptions>()
    .BindConfiguration(RiskModelOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddSingleton<IPathResolver, PathResolver>();
builder.Services.AddSingleton<IRiskModelService, RiskModelService>();

var app = builder.Build();

// Create the model service now, before the app accepts a request. A missing,
// corrupted, changed or incompatible model throws here and stops the start-up.
_ = app.Services.GetRequiredService<IRiskModelService>();
app.Logger.LogInformation("Risk model loaded.");

// An unexpected error returns a generic 500 body. It never shows a stack trace.
app.UseExceptionHandler();

app.MapRiskEndpoints();

app.Run();
