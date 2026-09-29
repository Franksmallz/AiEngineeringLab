namespace FoundationalModel.Models.Dtos.Requests
{
    public class SubmitFeedbackRequest
    {
        public required string InferenceId { get; init; }
        public required bool IsCorrect { get; init; }
        public string? CorrectCategory { get; init; }
        public bool? CorrectRetryable { get; init; }
        public string? CorrectAction { get; init; }
        public string? Comment { get; init; }

    }
}
