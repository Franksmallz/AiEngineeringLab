using System.Text.Json;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

public class ModelManifestTests
{
    private static string Json(Action<Dictionary<string, object?>>? edit = null)
    {
        var d = new Dictionary<string, object?>
        {
            ["modelVersion"] = "chapter-06-v1",
            ["modelFile"] = "chapter-06-risk-classifier.zip",
            ["modelSha256"] = TestSupport.ModelSha256,
            ["threshold"] = TestSupport.Threshold,
            ["featureSchemaVersion"] = "risk-features-v1",
            ["decisionPolicyVersion"] = "risk-policy-v1"
        };
        edit?.Invoke(d);
        return JsonSerializer.Serialize(d);
    }

    [Fact]
    public void A_valid_manifest_parses()
    {
        var manifest = ModelManifest.Parse(Json());
        Assert.Equal("chapter-06-v1", manifest.ModelVersion);
        Assert.Equal(TestSupport.Threshold, manifest.Threshold);
    }

    [Fact]
    public void The_shipped_manifest_matches_the_chapter_06_policy_file()
    {
        var root = TestSupport.RepoRoot();
        var manifest = ModelManifest.Load(Path.Combine(root, TestSupport.ManifestPath));
        using var policy = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "Models", "chapter-06-risk-classifier.policy.json")));

        // The manifest repeats Chapter 6 facts. They must not drift.
        Assert.Equal(
            policy.RootElement.GetProperty("model").GetProperty("contentSha256").GetString(),
            manifest.ModelSha256);
        Assert.Equal(
            policy.RootElement.GetProperty("decision").GetProperty("threshold").GetDouble(),
            manifest.Threshold);
        Assert.Equal(
            Path.GetFileName(policy.RootElement.GetProperty("model").GetProperty("file").GetString()),
            manifest.ModelFile);
        Assert.Equal(policy.RootElement.GetProperty("policyVersion").GetString(), manifest.ModelVersion);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(-0.1)]
    public void An_invalid_threshold_is_rejected(double threshold)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ModelManifest.Parse(Json(d => d["threshold"] = threshold)));
        Assert.Contains("threshold", ex.Message);
    }

    [Theory]
    [InlineData("modelVersion", "")]
    [InlineData("modelFile", "")]
    [InlineData("modelFile", "../other.zip")]
    [InlineData("modelFile", "sub/model.zip")]
    [InlineData("modelSha256", "abc")]
    [InlineData("modelSha256", "")]
    [InlineData("featureSchemaVersion", "risk-features-v2")]
    [InlineData("featureSchemaVersion", "")]
    [InlineData("decisionPolicyVersion", "risk-policy-v2")]
    [InlineData("decisionPolicyVersion", "")]
    public void An_invalid_field_is_rejected(string field, string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ModelManifest.Parse(Json(d => d[field] = value)));
        Assert.Contains(field, ex.Message);
    }

    [Fact]
    public void A_missing_field_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => ModelManifest.Parse(Json(d => d.Remove("modelSha256"))));

    [Fact]
    public void An_unknown_field_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => ModelManifest.Parse(Json(d => d["extra"] = 1)));

    [Fact]
    public void Several_problems_are_all_reported()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ModelManifest.Parse(Json(d =>
        {
            d["threshold"] = 2.0;
            d["modelSha256"] = "abc";
        })));
        Assert.Contains("threshold", ex.Message);
        Assert.Contains("modelSha256", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("null")]
    public void Unreadable_content_is_rejected(string content) =>
        Assert.Throws<InvalidOperationException>(() => ModelManifest.Parse(content));

    [Fact]
    public void A_missing_manifest_file_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => ModelManifest.Load(Path.Combine(Path.GetTempPath(), "no-such-manifest.json")));
}
