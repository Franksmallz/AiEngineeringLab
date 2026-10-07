namespace RiskClassificationLab.Models
{
    public class SuspiciousTransactions
    {
        public SuspiciousTransactionData SuspiciousMedium { get; set; } = new SuspiciousTransactionData();
        public SuspiciousTransactionData SuspiciousHigh { get; set; } = new SuspiciousTransactionData();
    }

    public class SuspiciousTransactionData
    {
        public List<TransactionRiskData> Records { get; set; } = new List<TransactionRiskData>();
        public int TotalCount { get; set; }
    }
}
