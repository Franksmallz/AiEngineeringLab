using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;

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
