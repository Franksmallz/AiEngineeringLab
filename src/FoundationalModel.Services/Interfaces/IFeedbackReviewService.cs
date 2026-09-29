using FoundationalModel.Models.Entities;

namespace FoundationalModel.Services.Interfaces
{
    public interface IFeedbackReviewService : IAutoDependencyService
    {
        Task ApproveAsync(string reviewId, string reviewerComment, CancellationToken cancellationToken = default);
        Task<IEnumerable<FeedbackReviewItem>> PendingReviews(CancellationToken cancellationToken = default);
        Task RejectAsync(string reviewId, string reviewerComment, CancellationToken cancellationToken = default);
    }
}
