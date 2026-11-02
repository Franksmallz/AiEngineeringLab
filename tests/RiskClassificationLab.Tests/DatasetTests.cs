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
    public void Generator_default_seed_is_42_and_other_seeds_differ()
    {
        var defaultSeed = new RiskDatasetGenerator().Generate(25);
        var seed42 = new RiskDatasetGenerator(seed: 42).Generate(25);
        var seed7 = new RiskDatasetGenerator(seed: 7).Generate(25);

        Assert.Equal(defaultSeed, seed42, new TransactionComparer());
        Assert.NotEqual(seed42, seed7, new TransactionComparer());
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
    public void Stratified_k_fold_puts_every_record_in_exactly_one_fold()
    {
        var data = ClassData(low: 2567, medium: 2209, high: 224);

        var folds = new DatasetSplitter().StratifiedKFold(data);

        Assert.Equal(5, folds.Count);
        Assert.Equal(5000, folds.Sum(f => f.Count));
        var all = folds.SelectMany(f => f).ToList();
        Assert.Equal(all.Count, all.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.True(data.ToHashSet(ReferenceEqualityComparer.Instance).SetEquals(all));
    }

    [Fact]
    public void Stratified_k_fold_keeps_each_class_within_one_record_across_folds()
    {
        var data = ClassData(low: 2567, medium: 2209, high: 224);

        var folds = new DatasetSplitter().StratifiedKFold(data);

        foreach (var (level, min, max) in new[] { ("Low", 513, 514), ("Medium", 441, 442), ("High", 44, 45) })
            Assert.All(folds, f => Assert.InRange(f.Count(x => x.RiskLevel == level), min, max));
        Assert.All(folds, f => Assert.Equal(1000, f.Count));
    }

    [Fact]
    public void Stratified_k_fold_is_deterministic_per_seed_and_leaves_the_input_unchanged()
    {
        var data = ClassData(low: 50, medium: 40, high: 10);
        var before = data.ToList();
        var splitter = new DatasetSplitter();

        var first = splitter.StratifiedKFold(data, seed: 42);
        var second = splitter.StratifiedKFold(data, seed: 42);
        var other = splitter.StratifiedKFold(data, seed: 7);

        Assert.Equal(before, data);
        Assert.All(first.Zip(second), p => Assert.Equal(p.First, p.Second));
        Assert.False(first.Zip(other).All(p => p.First.SequenceEqual(p.Second)));
    }

    [Fact]
    public void Fold_split_trains_on_four_folds_and_validates_on_one()
    {
        var splitter = new DatasetSplitter();
        var folds = splitter.StratifiedKFold(ClassData(low: 50, medium: 40, high: 10));

        var (train, validation) = splitter.GetFoldSplit(folds, validationFold: 2);

        Assert.Equal(folds[2], validation);
        Assert.Equal(80, train.Count);
        Assert.Empty(train.Intersect(validation, ReferenceEqualityComparer.Instance));
        Assert.Throws<ArgumentOutOfRangeException>(() => splitter.GetFoldSplit(folds, 5));
    }

    [Fact]
    public void Nested_stratified_subsets_are_nested_stratified_and_deterministic()
    {
        var data = ClassData(low: 2054, medium: 1767, high: 179);
        var splitter = new DatasetSplitter();
        int[] sizes = [300, 600, 1200, 2400, 4000];

        var subsets = sizes.Select(size => splitter.NestedStratifiedSubset(data, size)).ToList();

        Assert.Equal(sizes, subsets.Select(s => s.Count));
        foreach (var (smaller, larger) in subsets.Zip(subsets.Skip(1)))
            Assert.True(smaller.ToHashSet(ReferenceEqualityComparer.Instance).IsSubsetOf(larger));
        foreach (var subset in subsets)
            foreach (var (level, count) in new[] { ("Low", 2054), ("Medium", 1767), ("High", 179) })
                Assert.InRange(subset.Count(x => x.RiskLevel == level), count * subset.Count / 4000.0 - 1, count * subset.Count / 4000.0 + 1);
        Assert.Equal(subsets[0], splitter.NestedStratifiedSubset(data, 300));
        Assert.Equal(data, subsets[^1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => splitter.NestedStratifiedSubset(data, 4001));
    }

    [Theory]
    [InlineData(3001, 6, 4, 1, 1, 1, 1)]   // new beneficiary, large amount, late night
    [InlineData(3000, 6, 5, 1, 0, 0, 0)]   // 3000 is not large; hour 5 is not late
    [InlineData(5000, 7, 0, 0, 1, 0, 1)]   // 7 days is not new
    [InlineData(100, 400, 23, 0, 0, 0, 0)]
    public void Engineered_features_use_the_generator_thresholds(
        float amount, float ageDays, float hour,
        float isNew, float isLarge, float newAndLarge, float isLateNight)
    {
        var output = new EngineeredFeatures();

        EngineeredFeatures.Map(
            new EngineeredFeatureInput { Amount = amount, BeneficiaryAgeDays = ageDays, TransactionHour = hour },
            output);

        Assert.Equal(isNew, output.IsNewBeneficiary);
        Assert.Equal(isLarge, output.IsLargeTransaction);
        Assert.Equal(newAndLarge, output.NewBeneficiaryLargeAmount);
        Assert.Equal(isLateNight, output.IsLateNight);
    }

    private static List<TransactionRiskData> ClassData(int low, int medium, int high) =>
        Enumerable.Repeat("Low", low)
            .Concat(Enumerable.Repeat("Medium", medium))
            .Concat(Enumerable.Repeat("High", high))
            .Select((level, i) => new TransactionRiskData { Amount = i, RiskLevel = level })
            .ToList();

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
