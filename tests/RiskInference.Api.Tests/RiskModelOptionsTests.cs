using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

public class RiskModelOptionsTests
{
    private static List<ValidationResult> Validate(RiskModelOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_options_pass() =>
        Assert.Empty(Validate(new RiskModelOptions { ManifestPath = "Models/x.manifest.json" }));

    [Fact]
    public void The_shipped_appsettings_are_valid()
    {
        var path = Path.Combine(TestSupport.RepoRoot(), "src", "RiskInference.Api", "appsettings.json");
        var options = new ConfigurationBuilder().AddJsonFile(path).Build()
            .GetSection(RiskModelOptions.SectionName).Get<RiskModelOptions>()!;

        Assert.Empty(Validate(options));
        Assert.Equal(TestSupport.ManifestPath, options.ManifestPath);
        // Chapter 7, Step 4: pooled inference is the measured choice.
        Assert.Equal(InferenceMode.Pooled, options.InferenceMode);
        Assert.Equal(30, options.ShutdownTimeoutSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void A_manifest_path_is_required(string? manifestPath) =>
        Assert.NotEmpty(Validate(new RiskModelOptions { ManifestPath = manifestPath! }));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(601)]
    public void Shutdown_timeout_must_be_from_1_to_600_seconds(int seconds) =>
        Assert.NotEmpty(Validate(new RiskModelOptions { ManifestPath = "x", ShutdownTimeoutSeconds = seconds }));
}
