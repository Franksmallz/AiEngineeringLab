using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Implementations.ML
{
    // High versus Not High with a threshold: a record is flagged High when its
    // High score is >= the threshold. The three-class model is unchanged; only
    // the decision made from its High score changes.
    public static class ThresholdOptimizer
    {
        // Metrics for one threshold.
        public static BinaryThresholdMetrics Evaluate(
            IReadOnlyCollection<(bool IsHigh, float ScoreHigh)> records, double threshold) =>
            Metrics(records.Select(r => (r.IsHigh, Flagged: r.ScoreHigh >= threshold)).ToList(), threshold);

        // Metrics for any High / Not High decision, e.g. the default rule
        // (flagged = predicted High). Threshold is null when there is none.
        public static BinaryThresholdMetrics Metrics(
            IReadOnlyCollection<(bool IsHigh, bool Flagged)> decisions, double? threshold = null)
        {
            var tp = decisions.Count(d => d.IsHigh && d.Flagged);
            var fp = decisions.Count(d => !d.IsHigh && d.Flagged);
            var fn = decisions.Count(d => d.IsHigh && !d.Flagged);
            return Build(threshold, decisions.Count, tp, fp, fn);
        }

        // Every candidate threshold. Cost only changes when the threshold
        // crosses a record's score, so the candidates are the distinct scores.
        // Records are sorted from the highest score down; lowering the
        // threshold to the next score flags that group, so the counts are
        // running totals and the sweep is one pass.
        public static List<BinaryThresholdMetrics> Sweep(IReadOnlyCollection<(bool IsHigh, float ScoreHigh)> records)
        {
            var totalHigh = records.Count(r => r.IsHigh);
            var sweep = new List<BinaryThresholdMetrics>();
            int tp = 0, fp = 0;

            foreach (var group in records.GroupBy(r => r.ScoreHigh).OrderByDescending(g => g.Key))
            {
                tp += group.Count(r => r.IsHigh);
                fp += group.Count(r => !r.IsHigh);
                sweep.Add(Build(group.Key, records.Count, tp, fp, totalHigh - tp));
            }

            return sweep;
        }

        // The rule: the lowest cost among thresholds that flag at most
        // maximumFlagRate of the records. If two thresholds cost the same,
        // keep the higher one, because it flags fewer records.
        public static BinaryThresholdMetrics Select(
            IReadOnlyCollection<(bool IsHigh, float ScoreHigh)> records, double maximumFlagRate) =>
            Sweep(records)
                .Where(m => m.HighFlagRate <= maximumFlagRate)
                .OrderBy(m => m.BusinessCost)
                .ThenByDescending(m => m.Threshold)
                .First();

        private static BinaryThresholdMetrics Build(double? threshold, int records, int tp, int fp, int fn)
        {
            var precision = tp + fp == 0 ? 0 : (double)tp / (tp + fp);
            var recall = tp + fn == 0 ? 0 : (double)tp / (tp + fn);

            return new BinaryThresholdMetrics
            {
                Threshold = threshold,
                Records = records,
                TruePositives = tp,
                FalsePositives = fp,
                FalseNegatives = fn,
                TrueNegatives = records - tp - fp - fn,
                HighPrecision = precision,
                HighRecall = recall,
                HighF1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall),
                BusinessCost = fn * ClassificationMetricsCalculator.FalseNegativeCost
                    + fp * ClassificationMetricsCalculator.FalsePositiveCost,
                HighFlagRate = records == 0 ? 0 : (double)(tp + fp) / records
            };
        }
    }
}
