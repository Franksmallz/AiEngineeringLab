using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Implementations.ML;
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
    public void GenerateLargeDataset_writes_5000_train_and_1000_test_rows_without_duplicates()
    {
        var writer = new CapturingWriter();
        var service = CreateService(writer: writer);

        var result = service.GenerateLargeDataset();

        Assert.Equal(["chapter-06/train-5000.csv", "chapter-06/test-1000.csv"], writer.Filenames);
        Assert.Equal(5000, result.Train.Total);
        Assert.Equal(1000, result.Test.Total);
        Assert.Equal(result.Train.Total, result.Train.Low + result.Train.Medium + result.Train.High);
        Assert.Equal(result.Test.Total, result.Test.Low + result.Test.Medium + result.Test.High);
        Assert.Equal(0, result.Train.DuplicateRecords);
        Assert.Equal(0, result.Test.DuplicateRecords);
        Assert.Equal(0, result.CrossDatasetDuplicates);
    }

    [Fact]
    public void GenerateLargeDataset_is_reproducible()
    {
        var first = CreateService().GenerateLargeDataset();
        var second = CreateService().GenerateLargeDataset();

        Assert.Equal(
            (first.Train.Low, first.Train.Medium, first.Train.High, first.Test.Low, first.Test.Medium, first.Test.High),
            (second.Train.Low, second.Train.Medium, second.Train.High, second.Test.Low, second.Test.Medium, second.Test.High));
    }

    [Fact]
    public void EvaluateSdcaCrossValidation_predicts_every_record_once_and_writes_the_predictions()
    {
        var data = new RiskDatasetGenerator(seed: 42).Generate(300);
        var writer = new CapturingWriter();
        var service = CreateService(new StubReader(data), writer: writer);

        var result = service.EvaluateSdcaCrossValidation();

        Assert.Equal(5, result.FoldCount);
        Assert.Equal(300, result.OutOfFoldPredictionCount);
        Assert.Equal(300, result.Aggregate.Records);
        Assert.All(result.Folds, f => Assert.Equal(240, f.TrainingRecords));
        Assert.All(result.Folds, f => Assert.Equal(60, f.ValidationRecords));
        Assert.Equal(["High", "Low", "Medium"], result.ScoreOrdering.Order());
        Assert.True(result.ScoreOrderingSameInEveryFold);
        Assert.All(result.Predictions, p => Assert.InRange(p.Scores.Values.Sum(), 0.99f, 1.01f));
        Assert.All(result.Predictions, p => Assert.Equal(p.Scores.MaxBy(s => s.Value).Key, p.Predicted));
        var written = Assert.Single(writer.Predictions);
        Assert.Equal("evaluations/risk-classification/sdca-cross-validation-predictions.csv", written.Path);
        Assert.Equal(300, written.Count);
    }

    [Fact]
    public void EvaluateSdcaCrossValidation_repeats_every_label_and_score_exactly()
    {
        var data = new RiskDatasetGenerator(seed: 42).Generate(300);

        var first = CreateService(new StubReader(data)).EvaluateSdcaCrossValidation();
        var second = CreateService(new StubReader(data)).EvaluateSdcaCrossValidation();

        Assert.Equal(first.Predictions.Select(p => p.Predicted), second.Predictions.Select(p => p.Predicted));
        Assert.Equal(first.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)),
            second.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)));
    }

    [Fact]
    public void EvaluateSdcaEngineeredCrossValidation_adds_four_features_and_repeats_exactly()
    {
        var data = new RiskDatasetGenerator(seed: 42).Generate(300);
        var writer = new CapturingWriter();

        var first = CreateService(new StubReader(data), writer: writer).EvaluateSdcaEngineeredCrossValidation();
        var second = CreateService(new StubReader(data)).EvaluateSdcaEngineeredCrossValidation();
        var original = CreateService(new StubReader(data)).EvaluateSdcaCrossValidation();

        Assert.Equal("Engineered", first.FeatureSet);
        Assert.Equal(10, first.Features.Count);
        Assert.Equal(original.Features, first.Features.Take(6));
        Assert.Equal(original.Predictions.Select(p => (p.Fold, p.Actual)), first.Predictions.Select(p => (p.Fold, p.Actual)));
        Assert.Equal(first.Predictions.Select(p => p.Predicted), second.Predictions.Select(p => p.Predicted));
        Assert.Equal(first.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)),
            second.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)));
        Assert.Equal("evaluations/risk-classification/sdca-engineered-cross-validation-predictions.csv", Assert.Single(writer.Predictions).Path);
    }

    [Fact]
    public void EvaluateSdcaEngineeredNormalizedCrossValidation_only_adds_scaling_and_repeats_exactly()
    {
        var data = new RiskDatasetGenerator(seed: 42).Generate(300);
        var writer = new CapturingWriter();

        var first = CreateService(new StubReader(data), writer: writer).EvaluateSdcaEngineeredNormalizedCrossValidation();
        var second = CreateService(new StubReader(data)).EvaluateSdcaEngineeredNormalizedCrossValidation();
        var unscaled = CreateService(new StubReader(data)).EvaluateSdcaEngineeredCrossValidation();

        Assert.Equal("none", unscaled.FeatureScaling);
        Assert.StartsWith("NormalizeMeanVariance", first.FeatureScaling);
        Assert.Equal(unscaled.Features, first.Features);
        Assert.Equal(unscaled.Predictions.Select(p => (p.Fold, p.Actual)), first.Predictions.Select(p => (p.Fold, p.Actual)));
        Assert.Equal(first.Predictions.Select(p => p.Predicted), second.Predictions.Select(p => p.Predicted));
        Assert.Equal(first.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)),
            second.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)));
        Assert.Equal("evaluations/risk-classification/sdca-engineered-normalized-cross-validation-predictions.csv", Assert.Single(writer.Predictions).Path);
    }

    [Fact]
    public void Final_test_and_model_selection_refuse_to_run_after_the_test_set_was_used()
    {
        var store = new StubModelStore(["evaluations/risk-classification/chapter-06-final-test-results.json"]);
        var service = CreateService(store: store);

        Assert.Throws<InvalidOperationException>(() => service.EvaluateFinalModelOnTestSet());
        Assert.Throws<InvalidOperationException>(() => service.SelectFinalModel());
    }

    [Fact]
    public void EvaluateSdcaNestedThreshold_chooses_each_threshold_without_the_locked_fold()
    {
        var data = new RiskDatasetGenerator(seed: 42).Generate(300);
        var writer = new CapturingWriter();

        var result = CreateService(new StubReader(data), writer: writer).EvaluateSdcaNestedThreshold();
        var step5 = CreateService(new StubReader(data)).EvaluateSdcaEngineeredNormalizedCrossValidation();

        Assert.Equal(5, result.Folds.Count);
        Assert.All(result.Folds, f => Assert.Equal(240, f.InnerRecords));
        Assert.All(result.Folds, f => Assert.Equal([180, 180, 180, 180], f.InnerTrainingRecords));
        Assert.All(result.Folds, f => Assert.Equal(240, f.OuterTrainingRecords));
        Assert.All(result.Folds, f => Assert.True(f.Inner.HighFlagRate <= 0.40));
        Assert.All(result.Folds, f => Assert.Equal(60, f.Outer.Records));
        Assert.Equal(300, result.NestedAggregate.Records);
        Assert.Equal(result.Folds.Sum(f => f.Outer.BusinessCost), result.NestedAggregate.BusinessCost);
        Assert.Equal((step5.Aggregate.TruePositives, step5.Aggregate.FalsePositives, step5.Aggregate.FalseNegatives),
            (result.DefaultRule.TruePositives, result.DefaultRule.FalsePositives, result.DefaultRule.FalseNegatives));
        Assert.Equal("evaluations/risk-classification/sdca-engineered-normalized-threshold-sweep.csv", Assert.Single(writer.Sweeps).Path);
    }

    [Fact]
    public void EvaluateSdcaNormalizedCrossValidation_keeps_six_features_and_repeats_exactly()
    {
        var data = new RiskDatasetGenerator(seed: 42).Generate(300);
        var writer = new CapturingWriter();

        var first = CreateService(new StubReader(data), writer: writer).EvaluateSdcaNormalizedCrossValidation();
        var second = CreateService(new StubReader(data)).EvaluateSdcaNormalizedCrossValidation();
        var baseline = CreateService(new StubReader(data)).EvaluateSdcaCrossValidation();

        Assert.Equal("Original", first.FeatureSet);
        Assert.StartsWith("NormalizeMeanVariance", first.FeatureScaling);
        Assert.Equal(baseline.Features, first.Features);
        Assert.Equal(baseline.Predictions.Select(p => (p.Fold, p.Actual)), first.Predictions.Select(p => (p.Fold, p.Actual)));
        Assert.Equal(first.Predictions.Select(p => p.Predicted), second.Predictions.Select(p => p.Predicted));
        Assert.Equal(first.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)),
            second.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)));
        Assert.Equal("evaluations/risk-classification/sdca-normalized-cross-validation-predictions.csv", Assert.Single(writer.Predictions).Path);
    }

    [Fact]
    public void EvaluateLightGbmCrossValidation_uses_the_same_folds_and_repeats_exactly()
    {
        var data = new RiskDatasetGenerator(seed: 42).Generate(300);
        var writer = new CapturingWriter();

        var first = CreateService(new StubReader(data), writer: writer).EvaluateLightGbmCrossValidation();
        var second = CreateService(new StubReader(data)).EvaluateLightGbmCrossValidation();
        var sdca = CreateService(new StubReader(data)).EvaluateSdcaCrossValidation();

        Assert.Equal(300, first.OutOfFoldPredictionCount);
        Assert.Equal(sdca.Predictions.Select(p => (p.Fold, p.Actual)), first.Predictions.Select(p => (p.Fold, p.Actual)));
        Assert.Equal(first.Predictions.Select(p => p.Predicted), second.Predictions.Select(p => p.Predicted));
        Assert.Equal(first.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)),
            second.Predictions.SelectMany(p => p.Scores.OrderBy(s => s.Key)));
        Assert.All(first.Predictions, p => Assert.Equal(p.Scores.MaxBy(s => s.Value).Key, p.Predicted));
        Assert.Equal("evaluations/risk-classification/lightgbm-cross-validation-predictions.csv", Assert.Single(writer.Predictions).Path);
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

    [Fact]
    public void PredictWithScores_uses_the_ml_classifier()
    {
        var expected = new TransactionRiskPrediction { RiskLevel = "High", Score = [0.2f, 0.8f] };
        var resolver = new MappingResolver(new StubClassifier(), new StubClassifier(expected));
        var service = CreateService(resolver: resolver);

        var result = service.PredictWithScores(new TransactionRiskInput());

        Assert.Same(expected, result);
    }

    [Fact]
    public void Score_metrics_use_the_high_risk_score_and_business_costs()
    {
        var data = new List<TransactionRiskData>
        {
            new() { Amount = 1, RiskLevel = "High" },
            new() { Amount = 2, RiskLevel = "Low" },
            new() { Amount = 3, RiskLevel = "High" }
        };
        var resolver = new MappingResolver(
            new StubClassifier(),
            new AmountScoredClassifier(new Dictionary<float, float>
            {
                [1] = 0.9f,
                [2] = 0.8f,
                [3] = 0.2f
            }));
        var service = CreateService(new StubReader(data), resolver: resolver);

        var metrics = service.CalculateMetricsForThreshold(data, 0.5f);

        Assert.Equal((1, 1, 1, 0), (metrics.TP, metrics.FP, metrics.FN, metrics.TN));
        Assert.Equal(0.5, metrics.Precision);
        Assert.Equal(0.5, metrics.Recall);
        Assert.Equal(0.5, metrics.F1);
        Assert.Equal(1050, metrics.BusinessCost);
        Assert.Equal([0.2f, 0.8f, 0.9f], service.HighRiskScores());
    }

    [Fact]
    public void EvaluateWithScores_keeps_only_thresholds_within_review_capacity()
    {
        var data = new List<TransactionRiskData>
        {
            new() { Amount = 1, RiskLevel = "High" },
            new() { Amount = 2, RiskLevel = "Low" },
            new() { Amount = 3, RiskLevel = "High" }
        };
        var resolver = new MappingResolver(
            new StubClassifier(),
            new AmountScoredClassifier(new Dictionary<float, float>
            {
                [1] = 0.9f,
                [2] = 0.8f,
                [3] = 0.2f
            }));
        var service = CreateService(new StubReader(data), resolver: resolver);

        var result = service.EvaluateWithScores();

        var threshold = Assert.Single(result);
        Assert.Equal(0.9f, threshold.Threshold);
        Assert.True(threshold.meetsConstraints);
        Assert.Equal(1d / 3, threshold.flagRate);
    }

    [Fact]
    public void ProfileRiskDataset_delegates_to_the_dataset_profiler()
    {
        var expected = new List<ClassProfile>
        {
            new("High", 1, new(10, 10, 10), new(1, 1, 1), new(2, 2, 2), new(0, 0, 0), new(30, 30, 30), 1)
        };
        var profiler = new CapturingProfiler(expected);
        var service = CreateService(new StubReader([new() { RiskLevel = "High" }]), profiler: profiler);

        var result = service.ProfileDataset();

        Assert.Same(expected, result);
        Assert.Single(profiler.ReceivedData);
    }

    [Fact]
    public void EvaluateHighRiskOversampling_writes_data_trains_model_and_evaluates_it()
    {
        var writer = new CapturingWriter();
        var trainer = new CapturingTrainer();
        var reader = new FileAwareReader(
            [new() { RiskLevel = "Medium" }],
            [new() { RiskLevel = "High" }]);
        var resolver = new MappingResolver(new StubClassifier(), new StubClassifier("High"));
        var service = CreateService(reader, new StubGenerator([]), writer, trainer, resolver);

        var result = service.EvaluateHighRiskOversampling();

        var evaluation = Assert.Single(result);
        Assert.Equal("High", evaluation.Actual);
        Assert.Equal("High", evaluation.Predicted);
        Assert.Equal(["train_oversampled.csv"], writer.Filenames);
        Assert.Equal(("train_oversampled.csv", "oversampled_model.zip"), trainer.Request);
    }

    [Fact]
    public void FindSuspiciousTransactions_separates_medium_and_high_rule_exceptions()
    {
        var reader = new StubReader([
            new() { RiskLevel = "Medium", RecentFailureCount = 2, IsHighRiskCountry = true },
            new() { RiskLevel = "High", RecentFailureCount = 1, IsHighRiskCountry = false },
            new() { RiskLevel = "High", RecentFailureCount = 2, IsHighRiskCountry = false }
        ]);
        var service = CreateService(reader);

        var result = service.FindSuspiciousTransactions();

        Assert.Equal(1, result.SuspiciousMedium.TotalCount);
        Assert.Equal(1, result.SuspiciousHigh.TotalCount);
    }

    [Fact]
    public void FindHighRiskFalseNegatives_returns_only_actual_high_rows_not_predicted_high()
    {
        var reader = new StubReader([
            new() { Amount = 1, RiskLevel = "High" },
            new() { Amount = 2, RiskLevel = "High" },
            new() { Amount = 3, RiskLevel = "Medium" }
        ]);
        var resolver = new MappingResolver(
            new StubClassifier(),
            new AmountPredictionClassifier(new Dictionary<float, string>
            {
                [1] = "Low",
                [2] = "High",
                [3] = "Low"
            }));
        var service = CreateService(reader, resolver: resolver);

        var result = service.FindHighRiskFalseNegatives();

        var falseNegative = Assert.Single(result);
        Assert.Equal("High", falseNegative.Actual);
        Assert.Equal("Low", falseNegative.Predicted);
        Assert.Equal(1, falseNegative.Data.Amount);
    }

    private static RiskService CreateService(
        IDatasetReader? reader = null,
        IRiskDatasetGenerator? generator = null,
        IDatasetWriter? writer = null,
        IModelTrainer? trainer = null,
        IImplementationResolverService? resolver = null,
        IDatasetProfiler? profiler = null,
        ICrossValidationEvaluator? evaluator = null,
        IFinalModelStore? store = null) =>
        new(
            generator ?? new StubGenerator([]),
            new DatasetSplitter(),
            writer ?? new CapturingWriter(),
            reader ?? new StubReader([]),
            trainer ?? new CapturingTrainer(),
            resolver ?? new CapturingResolver(),
            profiler ?? new DatasetProfiler(),
            evaluator ?? new CrossValidationEvaluator(new DatasetSplitter()),
            store ?? new StubModelStore(existingFiles: []));

    private sealed class StubGenerator(List<TransactionRiskData> transactions) : IRiskDatasetGenerator
    {
        public List<TransactionRiskData> Generate(int count) => transactions;
        public List<TransactionRiskData> OversampleHighRisk(List<TransactionRiskData> trainingData) =>
            trainingData.Concat(trainingData.Where(x => x.RiskLevel == "High")).ToList();
        public TransactionRiskData GenerateRandomTransaction() =>
            transactions.FirstOrDefault() ?? new TransactionRiskData { RiskLevel = "High" };
    }

    private sealed class StubModelStore(HashSet<string> existingFiles) : IFinalModelStore
    {
        public string SaveModel(Microsoft.ML.MLContext mlContext, Microsoft.ML.ITransformer model, Microsoft.ML.DataViewSchema inputSchema, string configuredPath) =>
            throw new NotSupportedException();
        public Microsoft.ML.ITransformer LoadModel(Microsoft.ML.MLContext mlContext, string configuredPath, string expectedContentSha256) =>
            throw new NotSupportedException();
        public bool Exists(string configuredPath) => existingFiles.Contains(configuredPath);
        public void WriteJson<T>(string configuredPath, T value, bool overwrite) => throw new NotSupportedException();
        public T ReadJson<T>(string configuredPath) => throw new NotSupportedException();
    }

    private sealed class CapturingWriter : IDatasetWriter
    {
        public List<string> Filenames { get; } = [];
        public void CsvDatasetWriter(string filename, List<TransactionRiskData> data) => Filenames.Add(filename);
        public List<(string Path, int Count, List<string> ScoreOrdering)> Predictions { get; } = [];
        public void CsvPredictionWriter(string configuredPath, List<OutOfFoldPrediction> predictions, List<string> scoreOrdering) =>
            Predictions.Add((configuredPath, predictions.Count, scoreOrdering));
        public List<(string Path, int Count)> Sweeps { get; } = [];
        public void CsvThresholdSweepWriter(string configuredPath, List<BinaryThresholdMetrics> sweep) =>
            Sweeps.Add((configuredPath, sweep.Count));
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

    private sealed class FileAwareReader(
        List<TransactionRiskData> training,
        List<TransactionRiskData> evaluation) : IDatasetReader
    {
        public IEnumerable<TransactionRiskData> Read(string filename) =>
            filename == "evaluation.csv" ? evaluation : training;
    }

    private sealed class CapturingProfiler(List<ClassProfile> profile) : IDatasetProfiler
    {
        public List<TransactionRiskData> ReceivedData { get; private set; } = [];
        public List<ClassProfile> ProfileDataset(List<TransactionRiskData> data)
        {
            ReceivedData = data;
            return profile;
        }
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
        private readonly TransactionRiskPrediction _prediction;

        public StubClassifier(string result = "rules-result")
        {
            _result = result;
            _prediction = new TransactionRiskPrediction { RiskLevel = result };
        }

        public StubClassifier(TransactionRiskPrediction prediction)
        {
            _result = prediction.RiskLevel;
            _prediction = prediction;
        }

        public string Predict(TransactionRiskInput input) => _result;
        public TransactionRiskPrediction PredictWithScores(TransactionRiskInput input) => _prediction;
    }

    private sealed class AmountScoredClassifier(Dictionary<float, float> scores) : IRiskClassifier
    {
        public string Predict(TransactionRiskInput input) => "High";

        public TransactionRiskPrediction PredictWithScores(TransactionRiskInput input) => new()
        {
            RiskLevel = scores[input.Amount] >= 0.5f ? "High" : "Low",
            Score = [1 - scores[input.Amount], scores[input.Amount]]
        };
    }

    private sealed class AmountPredictionClassifier(Dictionary<float, string> predictions) : IRiskClassifier
    {
        public string Predict(TransactionRiskInput input) => predictions[input.Amount];
        public TransactionRiskPrediction PredictWithScores(TransactionRiskInput input) => new()
        {
            RiskLevel = predictions[input.Amount],
            Score = [0, 0]
        };
    }

    private sealed class MappingResolver(IRiskClassifier rules, IRiskClassifier ml) : IImplementationResolverService
    {
        public IRiskClassifier ResolveClassifier(string ruleProvider) =>
            ruleProvider.Equals("ML", StringComparison.OrdinalIgnoreCase) ? ml : rules;
    }
}
