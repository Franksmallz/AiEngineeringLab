using Microsoft.ML;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations.ML;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

public class RiskModelServiceTests
{
    private static readonly TransactionRiskData HighLooking = new()
    {
        Amount = 4500, TransactionHour = 2, CustomerTransactionCount24h = 12,
        RecentFailureCount = 3, BeneficiaryAgeDays = 2, IsHighRiskCountry = true
    };

    private static readonly TransactionRiskData LowLooking = new()
    {
        Amount = 80, TransactionHour = 13, CustomerTransactionCount24h = 1,
        RecentFailureCount = 0, BeneficiaryAgeDays = 400, IsHighRiskCountry = false
    };

    // ---- Startup: a bad manifest or model must stop the service ---------------

    [Fact]
    public void Loads_the_chapter_06_model_and_reports_the_active_versions()
    {
        using var service = TestSupport.CreateService();

        Assert.Equal(
            new ActiveModelInfo("chapter-06-v1", "risk-policy-v1", "risk-features-v1",
                TestSupport.ModelSha256, TestSupport.Threshold),
            service.Active);

        var response = service.Predict(HighLooking);
        Assert.Equal("chapter-06-v1", response.ModelVersion);
        Assert.Equal("risk-policy-v1", response.DecisionPolicyVersion);
        Assert.Equal(TestSupport.Threshold, response.Threshold);
        Assert.InRange(response.HighRiskScore, 0f, 1f);
    }

    [Fact]
    public void Missing_manifest_stops_startup() =>
        AssertLoadFails(TestSupport.Options(Path.Combine(Path.GetTempPath(), "no-such-manifest.json")), "does not exist");

    [Fact]
    public void Missing_model_file_stops_startup()
    {
        using var artifact = new TempArtifact(copyModel: false);
        AssertLoadFails(artifact.Options(), "model.zip");
    }

    [Fact]
    public void Hash_mismatch_stops_startup()
    {
        using var artifact = new TempArtifact(m => m["modelSha256"] = new string('0', 64));
        AssertLoadFails(artifact.Options(), "SHA-256");
    }

    [Fact]
    public void A_model_whose_content_changed_after_the_manifest_was_made_stops_startup()
    {
        using var artifact = new TempArtifact();
        ArtifactTamper.ChangeOneByteInTheLargestEntry(artifact.ModelFile);

        AssertLoadFails(artifact.Options(), "SHA-256");
    }

    [Fact]
    public void A_zip_with_new_timestamps_but_the_same_content_still_loads()
    {
        // The hash covers entry names and content, not zip metadata, so saving
        // the same model again (new timestamps) does not break the manifest.
        using var artifact = new TempArtifact();
        RewriteEveryEntryWithSameContent(artifact.ModelFile);

        using var service = artifact.CreateService();
        Assert.Equal(TestSupport.ModelSha256, service.Active.ModelSha256);
    }

    private static void RewriteEveryEntryWithSameContent(string zipFile)
    {
        using var archive = System.IO.Compression.ZipFile.Open(zipFile, System.IO.Compression.ZipArchiveMode.Update);
        foreach (var entry in archive.Entries.ToList())
        {
            byte[] content;
            using (var read = entry.Open())
            using (var buffer = new MemoryStream())
            {
                read.CopyTo(buffer);
                content = buffer.ToArray();
            }
            var name = entry.FullName;
            entry.Delete();
            using var write = archive.CreateEntry(name).Open();
            write.Write(content);
        }
    }

    [Fact]
    public void Corrupted_model_file_stops_startup()
    {
        using var artifact = new TempArtifact(copyModel: false);
        File.WriteAllText(artifact.ModelFile, "this is not a zip file");
        AssertLoadFails(artifact.Options());
    }

    [Fact]
    public void Incompatible_model_stops_startup()
    {
        // A valid ML.NET model with the right hash, but not the risk pipeline.
        using var artifact = new TempArtifact(copyModel: false);
        var ml = new MLContext();
        var data = ml.Data.LoadFromEnumerable(new[] { new Unrelated { A = 1f }, new Unrelated { A = 2f } });
        var model = ml.Transforms.Concatenate("Features", "A").Fit(data);
        var hash = new FinalModelStore(TestSupport.PathResolver()).SaveModel(ml, model, data.Schema, artifact.ModelFile);
        artifact.WriteManifest(m => m["modelSha256"] = hash);

        AssertLoadFails(artifact.Options());
    }

    public sealed class Unrelated { public float A { get; set; } }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(-0.2)]
    public void An_invalid_threshold_stops_startup(double threshold)
    {
        using var artifact = new TempArtifact(m => m["threshold"] = threshold);
        AssertLoadFails(artifact.Options(), "threshold");
    }

    [Theory]
    [InlineData("featureSchemaVersion", "risk-features-v2")]
    [InlineData("decisionPolicyVersion", "risk-policy-v2")]
    public void An_unsupported_version_stops_startup(string field, string value)
    {
        using var artifact = new TempArtifact(m => m[field] = value);
        AssertLoadFails(artifact.Options(), "not supported");
    }

    [Theory]
    [InlineData(InferenceMode.Pooled)]
    [InlineData(InferenceMode.EnginePerRequest)]
    public void Every_startup_failure_applies_to_both_inference_modes(InferenceMode mode)
    {
        using var artifact = new TempArtifact(m => m["modelSha256"] = new string('1', 64));
        AssertLoadFails(artifact.Options(mode), "SHA-256");
    }

    private static void AssertLoadFails(RiskModelOptions options, string? messagePart = null)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TestSupport.CreateService(options));
        Assert.Contains("could not be loaded", ex.Message);
        if (messagePart is not null) Assert.Contains(messagePart, ex.Message);
    }

    // ---- Versions come from the manifest -------------------------------------

    [Fact]
    public void The_response_carries_the_versions_and_threshold_of_the_manifest()
    {
        using var artifact = new TempArtifact(m => m["modelVersion"] = "my-model-v7");
        using var service = artifact.CreateService();

        var response = service.Predict(HighLooking);
        Assert.Equal("my-model-v7", response.ModelVersion);
        Assert.Equal("risk-policy-v1", response.DecisionPolicyVersion);
        Assert.Equal("my-model-v7", service.Active.ModelVersion);
    }

    // ---- Prediction behavior -------------------------------------------------

    [Fact]
    public void Repeated_predictions_are_identical()
    {
        using var service = TestSupport.CreateService();
        var first = service.Predict(HighLooking);

        for (var i = 0; i < 100; i++)
            Assert.Equal(first, service.Predict(HighLooking));
    }

    [Fact]
    public void A_restart_gives_identical_predictions()
    {
        // Two separate loads of the same artifact are two service starts.
        var inputs = new DatasetSample().ToList();
        using var first = TestSupport.CreateService();
        var before = inputs.Select(first.Predict).ToList();

        using var second = TestSupport.CreateService();
        var after = inputs.Select(second.Predict).ToList();

        Assert.Equal(before, after);
        Assert.Equal(first.Active, second.Active);
    }

    [Fact]
    public void Concurrent_predictions_match_the_sequential_results()
    {
        using var service = TestSupport.CreateService();
        var inputs = new[] { HighLooking, LowLooking }
            .Concat(new DatasetSample().Take(30)).ToArray();
        var expected = inputs.Select(service.Predict).ToArray();

        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 4000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            var index = i % inputs.Length;
            try
            {
                var actual = service.Predict(inputs[index]);
                if (actual != expected[index]) failures.Add($"input {index}: {actual} != {expected[index]}");
            }
            catch (Exception ex) { failures.Add($"input {index}: {ex.GetType().Name} {ex.Message}"); }
        });

        Assert.Empty(failures);
    }

    [Fact]
    public void Flag_is_exactly_score_at_or_above_the_threshold()
    {
        using var service = TestSupport.CreateService();
        var response = service.Predict(HighLooking);

        // Move the threshold to the exact score: the boundary must flag.
        using var atScore = new TempArtifact(m => m["threshold"] = (double)response.HighRiskScore);
        using var atBoundary = atScore.CreateService();
        Assert.True(atBoundary.Predict(HighLooking).FlaggedHighRisk);

        // Just above the score must not flag.
        using var aboveScore = new TempArtifact(m => m["threshold"] = Math.BitIncrement((double)response.HighRiskScore));
        using var above = aboveScore.CreateService();
        Assert.False(above.Predict(HighLooking).FlaggedHighRisk);
    }

    [Fact]
    public void Clearly_different_transactions_get_different_scores()
    {
        using var service = TestSupport.CreateService();
        Assert.True(service.Predict(HighLooking).HighRiskScore > service.Predict(LowLooking).HighRiskScore);
    }
}
