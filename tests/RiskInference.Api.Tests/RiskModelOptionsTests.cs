using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

public class RiskModelOptionsTests
{
    private static RiskModelOptions Bind(params (string Key, string? Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["RiskModel:ModelPath"] = "Models/chapter-06-risk-classifier.zip",
            ["RiskModel:ModelVersion"] = "chapter-06-v1",
            ["RiskModel:HighRiskThreshold"] = "0.1121373102068901",
            ["RiskModel:ModelContentSha256"] = new string('a', 64)
        };
        foreach (var (key, value) in overrides) values[$"RiskModel:{key}"] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build()
            .GetSection(RiskModelOptions.SectionName).Get<RiskModelOptions>()!;
    }

    private static List<ValidationResult> Validate(RiskModelOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_options_pass() => Assert.Empty(Validate(Bind()));

    [Fact]
    public void The_shipped_appsettings_are_valid_and_use_the_chapter_06_values()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "src", "RiskInference.Api", "appsettings.json");

        var options = new ConfigurationBuilder().AddJsonFile(path).Build()
            .GetSection(RiskModelOptions.SectionName).Get<RiskModelOptions>()!;

        Assert.Empty(Validate(options));
        Assert.Equal("chapter-06-v1", options.ModelVersion);
        Assert.Equal(0.1121373102068901, options.HighRiskThreshold);
        // Chapter 7, Step 4: pooled inference is the measured choice.
        Assert.Equal(InferenceMode.Pooled, options.InferenceMode);
    }

    [Theory]
    [InlineData("HighRiskThreshold", "0")]
    [InlineData("HighRiskThreshold", "1")]
    [InlineData("HighRiskThreshold", "1.5")]
    [InlineData("ModelPath", "")]
    [InlineData("ModelVersion", "")]
    [InlineData("ModelContentSha256", "abc")]
    public void Bad_values_fail_validation(string key, string value) =>
        Assert.NotEmpty(Validate(Bind((key, value))));
}
