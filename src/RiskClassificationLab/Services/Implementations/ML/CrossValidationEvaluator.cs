using Microsoft.ML;
using Microsoft.ML.Data;
using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using RiskClassificationLab.Services.Interfaces.ML;

namespace RiskClassificationLab.Services.Implementations.ML
{
    // k-fold cross-validation. For each fold k, a new model trains on the
    // other folds and predicts fold k. Every record is predicted exactly once,
    // by a model that never saw it (an "out-of-fold" prediction).
    //
    // The trainer is passed in as a function, so SDCA, LightGBM or any other
    // trainer runs through the same folds, features and metrics.
    public class CrossValidationEvaluator : ICrossValidationEvaluator
    {
        private readonly IDatasetSplitter _datasetSplitter;

        public CrossValidationEvaluator(IDatasetSplitter datasetSplitter)
        {
            _datasetSplitter = datasetSplitter;
        }

        public CrossValidationEvaluationResult Evaluate(
            string modelName,
            List<List<TransactionRiskData>> folds,
            Func<MLContext, IEstimator<ITransformer>> createTrainer,
            int seed = 42,
            FeatureSet featureSet = FeatureSet.Original,
            bool normalizeFeatures = false,
            int? trainingSize = null)
        {
            var predictions = new List<OutOfFoldPrediction>();
            var foldResults = new List<FoldEvaluation>();

            for (var k = 0; k < folds.Count; k++)
            {
                // A. Fold k validates (1,000 records); the other four train (4,000).
                var (train, validation) = _datasetSplitter.GetFoldSplit(folds, k);

                // A2. (Learning curves only) Train on a nested stratified subset
                // of the training records. The validation fold stays the same.
                if (trainingSize is int size)
                    train = _datasetSplitter.NestedStratifiedSubset(train, size, seed);

                // B. Train a new model. A new seeded MLContext per fold means no
                // state is shared between folds and every run repeats exactly.
                // Rows are shuffled with a seed, as in ModelTrainer, because SDCA
                // learns in passes over the data and row order can affect it.
                var mlContext = new MLContext(seed: seed);
                var trainingView = mlContext.Data.ShuffleRows(
                    mlContext.Data.LoadFromEnumerable(train), seed: seed);

                var model = BuildPipeline(mlContext, createTrainer(mlContext), featureSet, normalizeFeatures)
                    .Fit(trainingView);

                // C. Predict the held-out fold. Each row gets PredictedRiskLevel
                // (the class with the highest score) and Score (one value per
                // class, summing to 1).
                var scored = model.Transform(mlContext.Data.LoadFromEnumerable(validation));

                // D. Score is an unnamed array; find which slot is which class.
                var scoreOrdering = ReadScoreOrdering(scored);

                // E. Pair each prediction with its true label. CreateEnumerable
                // keeps the input order, so prediction i belongs to validation[i].
                // The scores are stored by class name, so no later code depends
                // on slot positions.
                var foldPredictions = mlContext.Data
                    .CreateEnumerable<TransactionRiskPrediction>(scored, reuseRowObject: false)
                    .Select((prediction, i) => new OutOfFoldPrediction
                    {
                        Fold = k + 1,
                        Actual = validation[i].RiskLevel,
                        Predicted = prediction.RiskLevel,
                        Scores = scoreOrdering
                            .Select((level, slot) => (level, slot))
                            .ToDictionary(x => x.level, x => prediction.Score[x.slot])
                    })
                    .ToList();

                // F. A dropped row would shift every label after it and make all
                // metrics wrong, so fail instead.
                if (foldPredictions.Count != validation.Count)
                    throw new InvalidOperationException(
                        $"Fold {k + 1}: {foldPredictions.Count} predictions for {validation.Count} records.");

                // G. Keep the predictions and this fold's own metrics.
                predictions.AddRange(foldPredictions);
                foldResults.Add(new FoldEvaluation
                {
                    Fold = k + 1,
                    TrainingRecords = train.Count,
                    ValidationRecords = validation.Count,
                    ScoreOrdering = scoreOrdering,
                    Metrics = ClassificationMetricsCalculator.Calculate(
                        foldPredictions.Select(p => (p.Actual, p.Predicted)).ToList())
                });
            }

            var firstOrdering = foldResults.FirstOrDefault()?.ScoreOrdering ?? [];

            return new CrossValidationEvaluationResult
            {
                Model = modelName,
                FeatureSet = featureSet.ToString(),
                Features = FeatureColumns(featureSet).ToList(),
                FeatureScaling = normalizeFeatures
                    ? "NormalizeMeanVariance, fitted on each fold's training partition"
                    : "none",
                Seed = seed,
                FoldCount = folds.Count,
                PredictionRule = "default class prediction (highest score); no threshold tuning",
                ScoreOrdering = firstOrdering,
                ScoreOrderingSameInEveryFold = foldResults.All(f => f.ScoreOrdering.SequenceEqual(firstOrdering)),
                OutOfFoldPredictionCount = predictions.Count,
                // The aggregate comes from all out-of-fold predictions together,
                // not from an average of the fold scores. With about 45 High
                // records per fold, averaging small ratios would add noise.
                Aggregate = ClassificationMetricsCalculator.Calculate(
                    predictions.Select(p => (p.Actual, p.Predicted)).ToList()),
                Folds = foldResults,
                Predictions = predictions
            };
        }

        private static readonly string[] OriginalFeatures =
        [
            nameof(TransactionRiskData.Amount),
            nameof(TransactionRiskData.TransactionHour),
            nameof(TransactionRiskData.CustomerTransactionCount24h),
            nameof(TransactionRiskData.RecentFailureCount),
            nameof(TransactionRiskData.BeneficiaryAgeDays),
            "IsHighRiskCountryFloat"
        ];

        private static readonly string[] EngineeredFeatureColumns =
        [
            nameof(EngineeredFeatures.IsNewBeneficiary),
            nameof(EngineeredFeatures.IsLargeTransaction),
            nameof(EngineeredFeatures.NewBeneficiaryLargeAmount),
            nameof(EngineeredFeatures.IsLateNight)
        ];

        internal static string[] FeatureColumns(FeatureSet featureSet) =>
            featureSet == FeatureSet.Engineered
                ? [.. OriginalFeatures, .. EngineeredFeatureColumns]
                : OriginalFeatures;

        // The feature pipeline. With FeatureSet.Original it is the six existing
        // features, as in ModelTrainer:
        // 1. MapValueToKey: the text label Low/Medium/High becomes a numeric key, "Label".
        // 2. ConvertType: IsHighRiskCountry true/false becomes 1.0/0.0.
        // 3. (Engineered only) CustomMapping adds the four 0/1 indicators.
        // 4. Concatenate: the features go into one vector, "Features".
        // 5. (normalizeFeatures only) NormalizeMeanVariance scales each feature
        //    in the vector. It is part of the pipeline, so Fit learns the
        //    scaling from the fold's training records only; the validation
        //    fold is scaled with those values and never seen during Fit.
        // 6. The trainer learns from Label and Features.
        // 7. MapKeyToValue: the predicted key becomes Low/Medium/High again.
        // Internal so the final model is built by exactly the same pipeline.
        internal static IEstimator<ITransformer> BuildPipeline(
            MLContext mlContext, IEstimator<ITransformer> trainer, FeatureSet featureSet, bool normalizeFeatures)
        {
            IEstimator<ITransformer> pipeline = mlContext.Transforms.Conversion
                .MapValueToKey(
                    outputColumnName: "Label",
                    inputColumnName: nameof(TransactionRiskData.RiskLevel))
                .Append(mlContext.Transforms.Conversion.ConvertType(
                    outputColumnName: "IsHighRiskCountryFloat",
                    inputColumnName: nameof(TransactionRiskData.IsHighRiskCountry),
                    outputKind: DataKind.Single));

            if (featureSet == FeatureSet.Engineered)
                pipeline = pipeline.Append(mlContext.Transforms.CustomMapping<EngineeredFeatureInput, EngineeredFeatures>(
                    EngineeredFeatures.Map, contractName: EngineeredFeatures.ContractName));

            pipeline = pipeline.Append(mlContext.Transforms.Concatenate("Features", FeatureColumns(featureSet)));

            if (normalizeFeatures)
                pipeline = pipeline.Append(mlContext.Transforms.NormalizeMeanVariance(
                    outputColumnName: "Features",
                    inputColumnName: "Features"));

            return pipeline
                .Append(trainer)
                .Append(mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedRiskLevel",
                    inputColumnName: "PredictedLabel"));
        }

        // ML.NET numbers the classes in the order it first meets them in the
        // training data, so Score[1] is not always the same class. The model
        // stores the class name of each Score slot ("slot names"); read them,
        // never assume them.
        internal static List<string> ReadScoreOrdering(IDataView scored)
        {
            VBuffer<ReadOnlyMemory<char>> slotNames = default;
            scored.Schema["Score"].GetSlotNames(ref slotNames);
            return slotNames.DenseValues().Select(x => x.ToString()).ToList();
        }
    }
}
