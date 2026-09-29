using FoundationalModel.Models.Entities;

namespace FoundationalModel.Repository.Interfaces
{
    public interface IFeedbackReviewRepository : IAutoDependencyRepository
    {
        Task SaveAsync(FeedbackReviewItem item, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<FeedbackReviewItem>> GetPendingAsync(CancellationToken cancellationToken = default);
        Task<FeedbackReviewItem?> GetAsync(string reviewId, CancellationToken cancellationToken = default);
    }
}
