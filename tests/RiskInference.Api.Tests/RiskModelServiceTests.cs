using Microsoft.ML;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations.ML;

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

    // ---- Startup: a bad model must stop the service -------------------------

    [Fact]
    public void Loads_the_chapter_06_model()
    {
        var response = TestSupport.CreateService().Predict(HighLooking);

        Assert.Equal("chapter-06-v1", response.ModelVersion);
        Assert.Equal(TestSupport.Threshold, response.Threshold);
        Assert.InRange(response.HighRiskScore, 0f, 1f);
    }

    [Fact]
    public void Missing_model_file_stops_startup() =>
        AssertLoadFails(TestSupport.Options(modelPath: "Models/does-not-exist.zip"));

    [Fact]
    public void Changed_model_hash_stops_startup() =>
        AssertLoadFails(TestSupport.Options(sha256: new string('0', 64)), "SHA-256");

    [Fact]
    public void Corrupted_model_file_stops_startup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"corrupt-{Guid.NewGuid():N}.zip");
        File.WriteAllText(path, "this is not a zip file");
        try { AssertLoadFails(TestSupport.Options(modelPath: path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Incompatible_model_stops_startup()
    {
        // A valid ML.NET model with the right hash, but not the risk pipeline.
        var path = Path.Combine(Path.GetTempPath(), $"incompatible-{Guid.NewGuid():N}.zip");
        try
        {
            var ml = new MLContext();
            var data = ml.Data.LoadFromEnumerable(new[] { new Unrelated { A = 1f }, new Unrelated { A = 2f } });
            var model = ml.Transforms.Concatenate("Features", "A").Fit(data);
            var hash = new FinalModelStore(TestSupport.PathResolver()).SaveModel(ml, model, data.Schema, path);

            AssertLoadFails(TestSupport.Options(modelPath: path, sha256: hash));
        }
        finally { File.Delete(path); }
    }

    public sealed class Unrelated { public float A { get; set; } }

    private static void AssertLoadFails(RiskInference.Api.Model.RiskModelOptions options, string? messagePart = null)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TestSupport.CreateService(options));
        Assert.Contains("could not be loaded", ex.Message);
        if (messagePart is not null) Assert.Contains(messagePart, ex.Message);
    }

    // ---- Prediction behavior -------------------------------------------------

    [Fact]
    public void Repeated_predictions_are_identical()
    {
        var service = TestSupport.CreateService();
        var first = service.Predict(HighLooking);

        for (var i = 0; i < 100; i++)
            Assert.Equal(first, service.Predict(HighLooking));
    }

    [Fact]
    public void Concurrent_predictions_match_the_sequential_results()
    {
        var service = TestSupport.CreateService();
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
        var service = TestSupport.CreateService();
        var response = service.Predict(HighLooking);

        // Move the threshold to the exact score: the boundary must flag.
        var atBoundary = TestSupport.CreateService(TestSupport.Options(threshold: response.HighRiskScore));
        Assert.True(atBoundary.Predict(HighLooking).FlaggedHighRisk);

        // Just above the score must not flag.
        var above = TestSupport.CreateService(
            TestSupport.Options(threshold: Math.BitIncrement((double)response.HighRiskScore)));
        Assert.False(above.Predict(HighLooking).FlaggedHighRisk);
    }

    [Fact]
    public void Clearly_different_transactions_get_different_scores()
    {
        var service = TestSupport.CreateService();
        Assert.True(service.Predict(HighLooking).HighRiskScore > service.Predict(LowLooking).HighRiskScore);
    }
}
