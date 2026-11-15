using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

internal static class TestSupport
{
    public const string ModelPath = "Models/chapter-06-risk-classifier.zip";
    public const string ModelSha256 = "0fd45f32162ab491340192b4023e1990a3d13935cd050f7b133745e8e309b571";
    public const double Threshold = 0.1121373102068901;

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "RiskInference.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // PathResolver walks up from the content root to the .slnx file.
    public static IPathResolver PathResolver() => new PathResolver(new TestEnvironment());

    public static RiskModelOptions Options(
        string? modelPath = null, string? sha256 = null, double threshold = Threshold,
        InferenceMode mode = InferenceMode.EnginePerRequest) => new()
    {
        ModelPath = modelPath ?? ModelPath,
        ModelVersion = "chapter-06-v1",
        HighRiskThreshold = threshold,
        ModelContentSha256 = sha256 ?? ModelSha256,
        InferenceMode = mode
    };

    public static RiskModelService CreateService(RiskModelOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? Options()), PathResolver());
}
