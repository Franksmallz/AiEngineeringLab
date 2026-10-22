using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Implementations.ML
{
    // Turns (actual, predicted) pairs into the evaluation metrics.
    // High is the positive class, because High is what we must catch.
    public static class ClassificationMetricsCalculator
    {
        // A missed High (false negative) costs far more than a false alarm.
        public const float FalseNegativeCost = 1_000;
        public const float FalsePositiveCost = 50;

        private static readonly string[] Classes = ["Low", "Medium", "High"];

        public static ClassificationMetrics Calculate(IReadOnlyCollection<(string Actual, string Predicted)> predictions)
        {
            // TP: genuine High, predicted High.
            // FP: not High, predicted High (false alarm).
            // FN: genuine High, predicted Low or Medium (missed).
            var tp = predictions.Count(p => p.Actual == "High" && p.Predicted == "High");
            var fp = predictions.Count(p => p.Actual != "High" && p.Predicted == "High");
            var fn = predictions.Count(p => p.Actual == "High" && p.Predicted != "High");

            // Precision: of the records flagged High, the share that are High.
            // Recall: of the genuine High records, the share we flagged.
            // F1: one number that balances precision and recall.
            // Each "== 0 ? 0" stops a divide-by-zero, e.g. when nothing is flagged.
            var precision = tp + fp == 0 ? 0 : (double)tp / (tp + fp);
            var recall = tp + fn == 0 ? 0 : (double)tp / (tp + fn);
            var f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);

            return new ClassificationMetrics
            {
                Records = predictions.Count,
                // Correct predictions over all three classes.
                Accuracy = predictions.Count == 0 ? 0 : (double)predictions.Count(p => p.Actual == p.Predicted) / predictions.Count,
                TruePositives = tp,
                FalsePositives = fp,
                FalseNegatives = fn,
                HighPrecision = precision,
                HighRecall = recall,
                HighF1 = f1,
                BusinessCost = fn * FalseNegativeCost + fp * FalsePositiveCost,
                // The share of all traffic sent for review as High.
                HighFlagRate = predictions.Count == 0 ? 0 : (double)(tp + fp) / predictions.Count,
                // ConfusionMatrix[actual][predicted] = count. For example,
                // ConfusionMatrix["High"]["Medium"] is genuine High predicted Medium.
                ConfusionMatrix = Classes.ToDictionary(
                    actual => actual,
                    actual => Classes.ToDictionary(
                        predicted => predicted,
                        predicted => predictions.Count(p => p.Actual == actual && p.Predicted == predicted)))
            };
        }
    }
}
