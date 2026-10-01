namespace RiskClassificationLab.Models
{
    public class TransactionRiskEvaluationResult
    {
       public RiskEvaluationResult Rules { get; set; }
        public RiskEvaluationResult ML { get; set; }
    }

    public class RiskEvaluationResult
    {
        public int Total { get; set; }
        public double Correct { get; set; }
        public double InCorrect { get; set; }
        public double Accuracy { get; set; }
        public List<ConfusionMatrix> ConfusionMatrix { get; set; }
        public List<ClassMetrics> ClassMetrics { get; set; }
        public EvaluationLatencyResult Latency { get; set; }
    }

    public class EvaluationLatencyResult
    {
        public double AverageLatency { get; set; }
        public double P95Latency { get; set; }
    }
    public class ConfusionMatrix
    {
        public string Predicted { get; set; }
        public string Actual { get; set; }
        public int Count { get; set; }
    }
}
