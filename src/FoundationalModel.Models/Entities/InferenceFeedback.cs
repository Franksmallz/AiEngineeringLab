namespace FoundationalModel.Models.Entities
{
    public class InferenceFeedback
    {
        public required string FeedbackId { get; init; }
        public required string InferenceId { get; init; }
        public required bool IsCorrect { get; init; }
        public required string? CorrectCategory { get; init; }
        public required bool? CorrectRetryable { get; init; }
        public required string? CorrectAction { get; init; }
        public required string? Comment { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
