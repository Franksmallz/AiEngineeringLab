using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Tests;

public sealed class DatasetTests
{
    [Fact]
    public void Generator_is_deterministic_and_returns_requested_count()
    {
        var first = new RiskDatasetGenerator().Generate(25);
        var second = new RiskDatasetGenerator().Generate(25);

        Assert.Equal(first, second, new TransactionComparer());
        Assert.Equal(25, first.Count);
        Assert.All(first, transaction => Assert.Contains(transaction.RiskLevel, new[] { "Low", "Medium", "High" }));
    }

    [Fact]
    public void Splitter_preserves_items_and_splits_each_risk_group()
    {
        var data = new List<TransactionRiskData>
        {
            new() { RiskLevel = "Low" },
            new() { RiskLevel = "Low" },
            new() { RiskLevel = "Low" },
            new() { RiskLevel = "Medium" },
            new() { RiskLevel = "Medium" },
            new() { RiskLevel = "High" }
        };

        var (train, evaluation) = new DatasetSplitter().Split(data, 0.5);

        Assert.Equal(6, train.Count + evaluation.Count);
        Assert.Equal(2, train.Count(x => x.RiskLevel == "Low"));
        Assert.Equal(1, train.Count(x => x.RiskLevel == "Medium"));
        Assert.Equal(0, train.Count(x => x.RiskLevel == "High"));
        Assert.Equal(1, evaluation.Count(x => x.RiskLevel == "Low"));
        Assert.Equal(1, evaluation.Count(x => x.RiskLevel == "Medium"));
        Assert.Equal(1, evaluation.Count(x => x.RiskLevel == "High"));
    }

    [Fact]
    public void Generator_oversamples_high_risk_rows_to_the_medium_class_count()
    {
        var data = new List<TransactionRiskData>
        {
            new() { RiskLevel = "Medium" },
            new() { RiskLevel = "Medium" },
            new() { RiskLevel = "High" },
            new() { RiskLevel = "Low" }
        };

        var result = new RiskDatasetGenerator().OversampleHighRisk(data);

        Assert.Equal(5, result.Count);
        Assert.Equal(2, result.Count(x => x.RiskLevel == "High"));
        Assert.Equal(2, result.Count(x => x.RiskLevel == "Medium"));
    }

    [Fact]
    public void Generator_does_not_add_rows_when_high_risk_count_already_meets_target()
    {
        var data = new List<TransactionRiskData>
        {
            new() { RiskLevel = "Medium" },
            new() { RiskLevel = "High" },
            new() { RiskLevel = "High" }
        };

        var result = new RiskDatasetGenerator().OversampleHighRisk(data);

        Assert.Equal(data.Count, result.Count);
        Assert.Equal(2, result.Count(x => x.RiskLevel == "High"));
    }

    [Fact]
    public void Dataset_profiler_calculates_class_counts_feature_ranges_and_country_rates()
    {
        var data = new List<TransactionRiskData>
        {
            new() { RiskLevel = "High", Amount = 10, TransactionHour = 2, CustomerTransactionCount24h = 4, RecentFailureCount = 1, BeneficiaryAgeDays = 3, IsHighRiskCountry = true },
            new() { RiskLevel = "High", Amount = 20, TransactionHour = 6, CustomerTransactionCount24h = 8, RecentFailureCount = 3, BeneficiaryAgeDays = 7, IsHighRiskCountry = false }
        };

        var profile = new DatasetProfiler().ProfileDataset(data);

        var high = Assert.Single(profile);
        Assert.Equal("High", high.RiskLevel);
        Assert.Equal(2, high.Count);
        Assert.Equal(15, high.Amount.Mean);
        Assert.Equal(10, high.Amount.Min);
        Assert.Equal(20, high.Amount.Max);
        Assert.Equal(0.5, high.HighRiskCountryRate);
    }

    private sealed class TransactionComparer : IEqualityComparer<TransactionRiskData>
    {
        public bool Equals(TransactionRiskData? x, TransactionRiskData? y) =>
            x is not null && y is not null &&
            x.Amount == y.Amount &&
            x.TransactionHour == y.TransactionHour &&
            x.CustomerTransactionCount24h == y.CustomerTransactionCount24h &&
            x.RecentFailureCount == y.RecentFailureCount &&
            x.BeneficiaryAgeDays == y.BeneficiaryAgeDays &&
            x.IsHighRiskCountry == y.IsHighRiskCountry &&
            x.RiskLevel == y.RiskLevel;

        public int GetHashCode(TransactionRiskData obj) => HashCode.Combine(
            obj.Amount, obj.TransactionHour, obj.CustomerTransactionCount24h,
            obj.RecentFailureCount, obj.BeneficiaryAgeDays,
            obj.IsHighRiskCountry, obj.RiskLevel);
    }
}
