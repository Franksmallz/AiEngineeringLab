using RiskClassificationLab.Services.Implementations.ML;

namespace RiskClassificationLab.Tests;

public sealed class ThresholdOptimizerTests
{
    // A: 0.03, B: 0.11 (High), C: 0.12, D: 0.40 (High), E: 0.85 (High), F: 0.90 (High)
    private static readonly List<(bool IsHigh, float ScoreHigh)> Example =
    [
        (false, 0.03f), (true, 0.11f), (false, 0.12f), (true, 0.40f), (true, 0.85f), (true, 0.90f)
    ];

    [Fact]
    public void Sweep_counts_every_distinct_score_from_the_highest_down()
    {
        var sweep = ThresholdOptimizer.Sweep(Example);

        Assert.Equal([0.90f, 0.85f, 0.40f, 0.12f, 0.11f, 0.03f], sweep.Select(m => (float)m.Threshold!.Value));
        Assert.Equal([3000.0, 2000.0, 1000.0, 1050.0, 50.0, 100.0], sweep.Select(m => m.BusinessCost));
        Assert.Equal(sweep, Example.Select(r => (double)r.ScoreHigh).OrderDescending()
            .Select(t => ThresholdOptimizer.Evaluate(Example, t)), new MetricsComparer());
    }

    [Fact]
    public void Select_takes_the_lowest_cost_within_the_flag_rate_limit()
    {
        var unlimited = ThresholdOptimizer.Select(Example, maximumFlagRate: 1.0);
        var limited = ThresholdOptimizer.Select(Example, maximumFlagRate: 0.40);

        Assert.Equal(0.11f, (float)unlimited.Threshold!.Value);
        Assert.Equal((4, 1, 0, 50.0), (unlimited.TruePositives, unlimited.FalsePositives, unlimited.FalseNegatives, unlimited.BusinessCost));
        Assert.Equal(0.85f, (float)limited.Threshold!.Value);
        Assert.True(limited.HighFlagRate <= 0.40);
    }

    [Fact]
    public void Select_keeps_the_higher_threshold_when_costs_are_equal()
    {
        // 0.9 flags nothing wrong but misses one High: $1,000.
        // 0.5 catches it with 20 false alarms: 20 x $50 = $1,000.
        var records = new List<(bool IsHigh, float ScoreHigh)> { (true, 0.9f), (true, 0.5f) };
        records.AddRange(Enumerable.Repeat((false, 0.6f), 20));
        records.AddRange(Enumerable.Repeat((false, 0.1f), 100));

        var chosen = ThresholdOptimizer.Select(records, maximumFlagRate: 1.0);

        Assert.Equal(1000, chosen.BusinessCost);
        Assert.Equal(0.9f, (float)chosen.Threshold!.Value);
    }

    private sealed class MetricsComparer : IEqualityComparer<RiskClassificationLab.Models.BinaryThresholdMetrics>
    {
        public bool Equals(RiskClassificationLab.Models.BinaryThresholdMetrics? x, RiskClassificationLab.Models.BinaryThresholdMetrics? y) =>
            x is not null && y is not null &&
            (x.Threshold, x.TruePositives, x.FalsePositives, x.FalseNegatives, x.TrueNegatives, x.BusinessCost, x.HighFlagRate)
            == (y.Threshold, y.TruePositives, y.FalsePositives, y.FalseNegatives, y.TrueNegatives, y.BusinessCost, y.HighFlagRate);

        public int GetHashCode(RiskClassificationLab.Models.BinaryThresholdMetrics obj) => obj.TruePositives;
    }
}
