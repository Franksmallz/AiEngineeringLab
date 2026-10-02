namespace RiskClassificationLab.Models
{
    public class TransactionRiskInput
    {
        public float Amount { get; set; }
        public float TransactionHour { get; set; }
        public float CustomerTransactionCount24h { get; set; }
        public float RecentFailureCount { get; set; }
        public float BeneficiaryAgeDays { get; set; }
        public bool IsHighRiskCountry { get; set; }
    }
}
