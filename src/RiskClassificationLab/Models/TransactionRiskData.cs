using Microsoft.ML.Data;

namespace RiskClassificationLab.Models
{
    public class TransactionRiskData
    {

        [LoadColumn(0)]
        public float Amount { get; set; }

        [LoadColumn(1)]
        public float TransactionHour { get; set; }

        [LoadColumn(2)]
        public float CustomerTransactionCount24h { get; set; }

        [LoadColumn(3)]
        public float RecentFailureCount { get; set; }

        [LoadColumn(4)]
        public float BeneficiaryAgeDays { get; set; }

        [LoadColumn(5)]
        public bool IsHighRiskCountry { get; set; }

        [LoadColumn(6)]
        public string RiskLevel { get; set; } = string.Empty;
    }
}
