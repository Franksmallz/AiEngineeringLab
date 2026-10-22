using Microsoft.ML;
using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces.ML
{
    public interface ICrossValidationEvaluator : IAutoDependencyService
    {
        // Trains a fresh model per fold (the other folds as training data) and
        // scores the held-out fold. The trainer reads "Label" and "Features".
        // normalizeFeatures adds NormalizeMeanVariance before the trainer.
        // trainingSize trains each fold on a nested stratified subset of its
        // training records; null uses all of them.
        CrossValidationEvaluationResult Evaluate(
            string modelName,
            List<List<TransactionRiskData>> folds,
            Func<MLContext, IEstimator<ITransformer>> createTrainer,
            int seed = 42,
            FeatureSet featureSet = FeatureSet.Original,
            bool normalizeFeatures = false,
            int? trainingSize = null);
    }
}
