namespace FoundationalModel.Models.Entities
{
    public class TrainingExampleCandidate
    {
        public required string CandidateId { get; init; }
        public required string InferenceId { get; init; }
        public required string FeedbackId { get; init; }
        public required string Input { get; init; }
        public required string ExpectedOutput { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
