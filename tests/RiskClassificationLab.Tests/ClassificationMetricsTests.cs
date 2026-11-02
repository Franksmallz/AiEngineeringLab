using RiskClassificationLab.Services.Implementations.ML;

namespace RiskClassificationLab.Tests;

public sealed class ClassificationMetricsTests
{
    [Fact]
    public void Calculator_treats_high_as_the_positive_class_and_prices_errors()
    {
        var predictions = new List<(string Actual, string Predicted)>
        {
            ("High", "High"),     // TP
            ("High", "Medium"),   // FN
            ("High", "Low"),      // FN
            ("Medium", "High"),   // FP
            ("Low", "Low"),
            ("Medium", "Medium"),
            ("Medium", "Low"),
            ("Low", "Medium")
        };

        var metrics = ClassificationMetricsCalculator.Calculate(predictions);

        Assert.Equal(8, metrics.Records);
        Assert.Equal(3.0 / 8, metrics.Accuracy);
        Assert.Equal(1, metrics.TruePositives);
        Assert.Equal(1, metrics.FalsePositives);
        Assert.Equal(2, metrics.FalseNegatives);
        Assert.Equal(0.5, metrics.HighPrecision);
        Assert.Equal(1.0 / 3, metrics.HighRecall, 10);
        Assert.Equal(0.4, metrics.HighF1, 10);
        Assert.Equal(2 * 1_000 + 1 * 50, metrics.BusinessCost);
        Assert.Equal(2.0 / 8, metrics.HighFlagRate);
        Assert.Equal(1, metrics.ConfusionMatrix["High"]["Low"]);
        Assert.Equal(1, metrics.ConfusionMatrix["Medium"]["High"]);
        Assert.Equal(8, metrics.ConfusionMatrix.Values.Sum(row => row.Values.Sum()));
    }

    [Fact]
    public void Calculator_returns_zero_rates_when_nothing_is_flagged_high()
    {
        var metrics = ClassificationMetricsCalculator.Calculate([("Low", "Low"), ("High", "Medium")]);

        Assert.Equal(0, metrics.HighPrecision);
        Assert.Equal(0, metrics.HighRecall);
        Assert.Equal(0, metrics.HighF1);
        Assert.Equal(0, metrics.HighFlagRate);
        Assert.Equal(1_000, metrics.BusinessCost);
    }
}
