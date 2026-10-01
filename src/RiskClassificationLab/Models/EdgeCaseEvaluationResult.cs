namespace RiskClassificationLab.Models
{
    public class EdgeCaseEvaluationResult
    {
        public EdgeCaseEvaluation AmountBoundary { get; set; }
        public EdgeCaseEvaluation TransactionCountBoundary { get; set; }
        public EdgeCaseEvaluation RecentFailureCountBoundary { get; set; }
        public EdgeCaseEvaluation BeneficiaryAgeDaysBoundary { get; set; }
        public EdgeCaseEvaluation TransactionHourBoundary { get; set; }
    }
    public class EdgeCaseEvaluation
    {
        public EdgeCasePrediction Below { get; set; }
        public EdgeCasePrediction Above { get; set; }
    }

    public class EdgeCasePrediction
    {
        public string Rules { get; set; }
        public string ML { get; set; }
    }
}
