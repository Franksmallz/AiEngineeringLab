using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Interfaces;
using RiskClassificationLab.Services.Interfaces.ML;

namespace RiskClassificationLab.Tests;

public sealed class RiskServiceTests
{
    [Fact]
    public void Generate_writes_split_datasets_and_reports_distributions()
    {
        var transactions = new List<TransactionRiskData>
        {
            new() { RiskLevel = "Low" },
            new() { RiskLevel = "Low" },
            new() { RiskLevel = "Medium" },
            new() { RiskLevel = "High" }
        };
        var writer = new CapturingWriter();
        var service = CreateService(
            generator: new StubGenerator(transactions),
            writer: writer);

        var result = service.Generate();

        Assert.Equal(4, result.Total.Count);
        Assert.Equal(2, result.Total.Distribution["Low"]);
        Assert.Equal(4, result.Train.Count);
        Assert.Equal(0, result.Evaluation.Count);
        Assert.Equal(["train.csv", "evaluation.csv"], writer.Filenames);
    }

    [Fact]
    public void Predict_and_ml_predict_use_the_expected_service_operations()
    {
        var resolver = new CapturingResolver();
        var service = CreateService(resolver: resolver);
        var input = new TransactionRiskInput();

        Assert.Equal("rules-result", service.Predict(input));
        Assert.Equal("rules-result", service.MLPredict(input));
        Assert.Equal(["Rules", "Rules"], resolver.RequestedKeys);
    }

    [Fact]
    public void Train_delegates_the_expected_dataset_and_model_paths()
    {
        var trainer = new CapturingTrainer();
        var service = CreateService(trainer: trainer);

        Assert.Equal("Model training completed successfully", service.Train());
        Assert.Equal(("train.csv", "risk-classifier.zip"), trainer.Request);
    }

    [Fact]
    public void Evaluate_computes_metrics_for_rules_and_ml_predictions()
    {
        var data = new List<TransactionRiskData>
        {
            new() { RiskLevel = "Low" },
            new() { RiskLevel = "Low" },
            new() { RiskLevel = "High" }
        };
        var resolver = new MappingResolver(
            new StubClassifier("Low"),
            new StubClassifier("High"));
        var service = CreateService(new StubReader(data), resolver: resolver);

        var result = service.Evaluate();

        Assert.Equal(3, result.Rules.Total);
        Assert.Equal(2, result.Rules.Correct);
        Assert.Equal(2d / 3, result.Rules.Accuracy);
        Assert.Equal(3, result.Rules.ConfusionMatrix.Sum(x => x.Count));
        Assert.Equal(3, result.ML.Total);
        Assert.Equal(1, result.ML.Correct);
        Assert.Equal(1d / 3, result.ML.Accuracy);
        Assert.NotNull(result.Rules.Latency);
        Assert.NotNull(result.ML.Latency);
    }

    [Fact]
    public void EvaluateEdgeCases_evaluates_all_five_boundary_pairs_with_both_classifiers()
    {
        var resolver = new MappingResolver(
            new StubClassifier("Rules"),
            new StubClassifier("ML"));
        var service = CreateService(resolver: resolver);

        var result = service.EvaluateEdgeCases();

        Assert.Equal("Rules", result.AmountBoundary.Below.Rules);
        Assert.Equal("ML", result.AmountBoundary.Above.ML);
        Assert.Equal("Rules", result.TransactionCountBoundary.Below.Rules);
        Assert.Equal("ML", result.TransactionCountBoundary.Above.ML);
        Assert.Equal("Rules", result.RecentFailureCountBoundary.Below.Rules);
        Assert.Equal("ML", result.RecentFailureCountBoundary.Above.ML);
        Assert.Equal("Rules", result.BeneficiaryAgeDaysBoundary.Below.Rules);
        Assert.Equal("ML", result.BeneficiaryAgeDaysBoundary.Above.ML);
        Assert.Equal("Rules", result.TransactionHourBoundary.Below.Rules);
        Assert.Equal("ML", result.TransactionHourBoundary.Above.ML);
    }

    private static RiskService CreateService(
        IDatasetReader? reader = null,
        IRiskDatasetGenerator? generator = null,
        IDatasetWriter? writer = null,
        IModelTrainer? trainer = null,
        IImplementationResolverService? resolver = null) =>
        new(
            generator ?? new StubGenerator([]),
            new DatasetSplitter(),
            writer ?? new CapturingWriter(),
            reader ?? new StubReader([]),
            trainer ?? new CapturingTrainer(),
            resolver ?? new CapturingResolver());

    private sealed class StubGenerator(List<TransactionRiskData> transactions) : IRiskDatasetGenerator
    {
        public List<TransactionRiskData> Generate(int count) => transactions;
    }

    private sealed class CapturingWriter : IDatasetWriter
    {
        public List<string> Filenames { get; } = [];
        public void CsvDatasetWriter(string filename, List<TransactionRiskData> data) => Filenames.Add(filename);
    }

    private sealed class CapturingTrainer : IModelTrainer
    {
        public (string Filename, string ModelPath) Request { get; private set; }
        public void Train(string filename, string modelPath) => Request = (filename, modelPath);
    }

    private sealed class StubReader(List<TransactionRiskData> data) : IDatasetReader
    {
        public IEnumerable<TransactionRiskData> Read(string filename) => data;
    }

    private sealed class CapturingResolver : IImplementationResolverService
    {
        public List<string> RequestedKeys { get; } = [];
        public IRiskClassifier ResolveClassifier(string ruleProvider)
        {
            RequestedKeys.Add(ruleProvider);
            return new StubClassifier();
        }
    }

    private sealed class StubClassifier : IRiskClassifier
    {
        private readonly string _result;

        public StubClassifier(string result = "rules-result") => _result = result;

        public string Predict(TransactionRiskInput input) => _result;
    }

    private sealed class MappingResolver(IRiskClassifier rules, IRiskClassifier ml) : IImplementationResolverService
    {
        public IRiskClassifier ResolveClassifier(string ruleProvider) =>
            ruleProvider.Equals("ML", StringComparison.OrdinalIgnoreCase) ? ml : rules;
    }
}
