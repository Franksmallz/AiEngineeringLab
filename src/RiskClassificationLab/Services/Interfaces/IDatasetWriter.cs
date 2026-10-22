using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IDatasetWriter  : IAutoDependencyService
    {
        void CsvDatasetWriter(string filename, List<TransactionRiskData> data);
        void CsvPredictionWriter(string configuredPath, List<OutOfFoldPrediction> predictions, List<string> scoreOrdering);
        void CsvThresholdSweepWriter(string configuredPath, List<BinaryThresholdMetrics> sweep);
    }
}
