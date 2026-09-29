namespace FoundationalModel.Models.Entities
{
    public class DeploymentMetrics
    {
        public long TotalInferences { get; set; }
        public long TotalLatencyMs { get; set; }
        public long Failures { get; set; }
        public long InvalidStructuredOutputs { get; set; }
        public long TotalFeedback { get; set; }
        public long PositiveFeedback { get; set; }
        public long NegativeFeedback { get; set; }
        public long ApprovedCorrections { get; set; }
        public long RejectedCorrections { get; set; }
        public double AverageLatencyMs => TotalInferences > 0 ? (double)TotalLatencyMs / TotalInferences : 0;
        public double FeebackRate => TotalFeedback > 0 ? (double)TotalFeedback / TotalInferences : 0;
        public double NegativeFeedbackRate => TotalFeedback > 0 ? (double)NegativeFeedback / TotalFeedback : 0;
        public double InvalidOutputRate => TotalInferences > 0 ? (double)InvalidStructuredOutputs / TotalInferences : 0;
    }
}
