using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

internal static class TestSupport
{
    public const string ManifestPath = "Models/chapter-06-risk-classifier.manifest.json";
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

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0) dir = dir.Parent;
        return dir!.FullName;
    }

    public static RiskModelOptions Options(
        string? manifestPath = null, InferenceMode mode = InferenceMode.Pooled) => new()
    {
        ManifestPath = manifestPath ?? ManifestPath,
        InferenceMode = mode
    };

    public static RiskModelService CreateService(RiskModelOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? Options()), PathResolver());

    public static RiskModelService CreateService(InferenceMode mode) => CreateService(Options(mode: mode));
}

// A temporary folder with a copy of the real model and a manifest that a test
// can change. It has the same layout as a deployment: manifest next to the model.
internal sealed class TempArtifact : IDisposable
{
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), $"risk-artifact-{Guid.NewGuid():N}");
    public string ManifestFile => Path.Combine(Directory, "manifest.json");
    public string ModelFile => Path.Combine(Directory, "model.zip");

    public TempArtifact(Action<Dictionary<string, object?>>? editManifest = null, bool copyModel = true)
    {
        System.IO.Directory.CreateDirectory(Directory);
        if (copyModel)
            File.Copy(Path.Combine(TestSupport.RepoRoot(), TestSupport.ModelPath), ModelFile);
        WriteManifest(editManifest);
    }

    public void WriteManifest(Action<Dictionary<string, object?>>? edit = null)
    {
        var manifest = new Dictionary<string, object?>
        {
            ["modelVersion"] = "test-v1",
            ["modelFile"] = "model.zip",
            ["modelSha256"] = TestSupport.ModelSha256,
            ["threshold"] = TestSupport.Threshold,
            ["featureSchemaVersion"] = ModelManifest.SupportedFeatureSchemaVersion,
            ["decisionPolicyVersion"] = ModelManifest.SupportedDecisionPolicyVersion
        };
        edit?.Invoke(manifest);
        File.WriteAllText(ManifestFile, JsonSerializer.Serialize(manifest));
    }

    public RiskModelOptions Options(InferenceMode mode = InferenceMode.Pooled) =>
        TestSupport.Options(ManifestFile, mode);

    public RiskModelService CreateService(InferenceMode mode = InferenceMode.Pooled) =>
        TestSupport.CreateService(Options(mode));

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch (IOException) { }
    }
}
