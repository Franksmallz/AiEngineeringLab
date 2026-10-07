namespace RiskClassificationLab.Models
{
    public class TransactionRiskPredictionEvaluation
    {
        public string Predicted { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
    }

    public class TransactionRiskPredictionEvaluationWithRecord : TransactionRiskPredictionEvaluation
    {
        public TransactionRiskData Data { get; set; } = new TransactionRiskData();
    }
}
