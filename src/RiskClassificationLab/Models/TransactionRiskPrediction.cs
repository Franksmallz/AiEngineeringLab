using Microsoft.ML.Data;

namespace RiskClassificationLab.Models
{
    public class TransactionRiskPrediction
    {
        [ColumnName("PredictedRiskLevel")]
        public string RiskLevel { get; set; } = string.Empty;
    }
}
