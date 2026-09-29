using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using System.Collections.Concurrent;

namespace FoundationalModel.Repository.Implementations
{
    public class FeedbackReviewRepository : IFeedbackReviewRepository
    {
        private readonly ConcurrentDictionary<string, FeedbackReviewItem> _item = new();

        public Task<FeedbackReviewItem?> GetAsync(string reviewId, CancellationToken cancellationToken = default)
        {
            _item.TryGetValue(reviewId, out var item);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyCollection<FeedbackReviewItem>> GetPendingAsync(CancellationToken cancellationToken = default)
        {
            var pending = _item.Values
                           .Where(x => x.Status == ReviewStatus.Pending)
                           .OrderBy(x => x.CreatedAt)
                           .ToList();

            return Task.FromResult<IReadOnlyCollection<FeedbackReviewItem>>(pending);
        }

        public Task SaveAsync(FeedbackReviewItem item, CancellationToken cancellationToken = default)
        {
            _item[item.ReviewId] = item;

            return Task.CompletedTask;
        }
    }
}
