using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using System.Globalization;
using System.Text;

namespace RiskClassificationLab.Services.Implementations
{
    public class DatasetWriter : IDatasetWriter
    {
        private readonly IPathResolver _pathResolver;

        public DatasetWriter(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public void CsvDatasetWriter(string filename, List<TransactionRiskData> data)
        {
            var root = _pathResolver.ResolveConfiguredPath("data/risk-classification");

            // filename may include a subfolder, e.g. "chapter-06/train-5000.csv"
            var path = Path.Combine(root, filename);
            var directory = Path.GetDirectoryName(path)!;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            //not to contaminate the data
            if (File.Exists(path)) return;

            var builder = new StringBuilder();

            builder.AppendLine(
                "Amount,TransactionHour,CustomerTransactionCount24h," +
                "RecentFailureCount,BeneficiaryAgeDays,IsHighRiskCountry,RiskLevel");

            foreach (var transaction in data)
            {
                builder.AppendLine(string.Join(",",
                    transaction.Amount.ToString(CultureInfo.InvariantCulture),
                    transaction.TransactionHour.ToString(CultureInfo.InvariantCulture),
                    transaction.CustomerTransactionCount24h.ToString(CultureInfo.InvariantCulture),
                    transaction.RecentFailureCount.ToString(CultureInfo.InvariantCulture),
                    transaction.BeneficiaryAgeDays.ToString(CultureInfo.InvariantCulture),
                    transaction.IsHighRiskCountry,
                    transaction.RiskLevel));
            }

            File.WriteAllText(path, builder.ToString());
        }

        // Model outputs, not training data: the file is rewritten on each run.
        public void CsvPredictionWriter(string configuredPath, List<OutOfFoldPrediction> predictions, List<string> scoreOrdering)
        {
            var path = _pathResolver.ResolveConfiguredPath(configuredPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var builder = new StringBuilder();
            builder.AppendLine("Fold,Actual,Predicted," + string.Join(",", scoreOrdering.Select(level => $"Score{level}")));

            foreach (var prediction in predictions)
            {
                builder.AppendLine(string.Join(",",
                    new[] { prediction.Fold.ToString(CultureInfo.InvariantCulture), prediction.Actual, prediction.Predicted }
                        .Concat(scoreOrdering.Select(level => prediction.Scores[level].ToString(CultureInfo.InvariantCulture)))));
            }

            File.WriteAllText(path, builder.ToString());
        }

        // One row per candidate threshold, highest first; rewritten on each run.
        public void CsvThresholdSweepWriter(string configuredPath, List<BinaryThresholdMetrics> sweep)
        {
            var path = _pathResolver.ResolveConfiguredPath(configuredPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var builder = new StringBuilder();
            builder.AppendLine("Threshold,TP,FP,FN,TN,Precision,Recall,F1,BusinessCost,FlagRate");

            foreach (var m in sweep)
            {
                builder.AppendLine(string.Join(",",
                    m.Threshold?.ToString(CultureInfo.InvariantCulture),
                    m.TruePositives, m.FalsePositives, m.FalseNegatives, m.TrueNegatives,
                    m.HighPrecision.ToString(CultureInfo.InvariantCulture),
                    m.HighRecall.ToString(CultureInfo.InvariantCulture),
                    m.HighF1.ToString(CultureInfo.InvariantCulture),
                    m.BusinessCost.ToString(CultureInfo.InvariantCulture),
                    m.HighFlagRate.ToString(CultureInfo.InvariantCulture)));
            }

            File.WriteAllText(path, builder.ToString());
        }
    }
}
