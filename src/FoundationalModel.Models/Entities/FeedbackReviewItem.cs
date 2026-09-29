using FoundationalModel.Core.Enums;

namespace FoundationalModel.Models.Entities
{
    public class FeedbackReviewItem
    {
        public required string ReviewId { get; init; }
        public required string FeedbackId { get; init; }
        public required string InferenceId { get; init; }
        public required ReviewStatus Status { get; set; }
        public string? ReviewerComment { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
        public  DateTimeOffset? ReviewAt { get; set; }
    }
}
