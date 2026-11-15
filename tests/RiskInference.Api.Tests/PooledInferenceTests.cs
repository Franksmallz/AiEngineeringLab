using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

// The pooled mode must give exactly the same answers as the engine-per-request
// mode. Only speed and memory use may differ.
public class PooledInferenceTests
{
    [Fact]
    public void Pooled_predictions_equal_engine_per_request_predictions_exactly()
    {
        using var perRequest = TestSupport.CreateService(InferenceMode.EnginePerRequest);
        using var pooled = TestSupport.CreateService(InferenceMode.Pooled);

        foreach (var record in new DatasetSample())
            Assert.Equal(perRequest.Predict(record), pooled.Predict(record));
    }

    [Fact]
    public void Concurrent_pooled_predictions_match_the_sequential_results()
    {
        using var pooled = TestSupport.CreateService(InferenceMode.Pooled);
        var inputs = new DatasetSample().Take(40).ToArray();
        var expected = inputs.Select(pooled.Predict).ToArray();

        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        // More threads than the pool keeps, so the pool also makes and drops extra engines.
        Parallel.For(0, 8000, new ParallelOptions { MaxDegreeOfParallelism = 64 }, i =>
        {
            var index = i % inputs.Length;
            try
            {
                var actual = pooled.Predict(inputs[index]);
                if (actual != expected[index]) failures.Add($"input {index}: {actual} != {expected[index]}");
            }
            catch (Exception ex) { failures.Add($"input {index}: {ex.GetType().Name} {ex.Message}"); }
        });

        Assert.Empty(failures);
    }
}
