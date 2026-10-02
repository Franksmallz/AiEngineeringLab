namespace RiskClassificationLab.Models
{
    public class TransactionRiskDataResult
    {
        public Data Train { get; set; }
        public Data Total { get; set; }
        public Data Evaluation { get; set; }
    }

    public class Data
    {
        public int Count { get; set; }
        public Dictionary<string, int> Distribution { get; set; }
    }
}
