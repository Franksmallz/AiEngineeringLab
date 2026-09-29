using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Entities;
using FoundationalModel.Models.Helper;
using FoundationalModel.Repository.Interfaces;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class FeedbackReviewService : IFeedbackReviewService
    {
        private readonly IFeedbackReviewRepository _feedbackReviewRepository;
        private readonly IInferenceFeedbackRepository _feedbackRepository;
        private readonly IInferenceRecordRepository _inferenceRecordRepository;
        private readonly ITrainingExampleCandidateRepository _trainingExampleCandidateRepository;
        private readonly IInferenceMetrics _inferenceMetrics;

        public FeedbackReviewService(IFeedbackReviewRepository feedbackReviewRepository,
            IInferenceFeedbackRepository feedbackRepository,
            IInferenceRecordRepository inferenceRecordRepository,
            ITrainingExampleCandidateRepository trainingExampleCandidateRepository,
            IInferenceMetrics inferenceMetrics)
        {
            _feedbackReviewRepository = feedbackReviewRepository;
            _feedbackRepository = feedbackRepository;
            _inferenceRecordRepository = inferenceRecordRepository;
            _trainingExampleCandidateRepository = trainingExampleCandidateRepository;
            _inferenceMetrics = inferenceMetrics;
        }

        public async Task ApproveAsync(string reviewId, string reviewerComment, CancellationToken cancellationToken = default)
        {
            var review = await _feedbackReviewRepository.GetAsync(reviewId, cancellationToken);

            if(review is null)
                throw new InvalidOperationException("Review item not found.");

            var inference  = await _inferenceRecordRepository.GetAsync(review.InferenceId, cancellationToken); 
            
            if(inference is null)
                throw new InvalidOperationException("Inference record not found.");

            var feedbackItems = await _feedbackRepository.GetByInferenceIdAsync(review.InferenceId, cancellationToken);

            var feedback = feedbackItems.FirstOrDefault(f => f.FeedbackId == review.FeedbackId);

            if(feedback.CorrectCategory is null || feedback.CorrectRetryable is null
                || feedback.CorrectAction is null)
            {
                throw new InvalidOperationException("Approved feedback must contain a complete corrected answer");
            }

            review.Status = ReviewStatus.Approved;
            review.ReviewerComment = reviewerComment;
            review.ReviewAt = DateTimeOffset.UtcNow;

            await _feedbackReviewRepository.SaveAsync(review, cancellationToken);
            _inferenceMetrics.RecordReviewOutcome(review.InferenceId, ReviewStatus.Approved);

            var expectedOutput = TrainingExampleFormatter.BuildExpectedOutput(feedback.CorrectCategory, feedback.CorrectRetryable.Value, feedback.CorrectAction);

            var candidate = new TrainingExampleCandidate
            {
                CandidateId = Guid.NewGuid().ToString(),
                InferenceId = review.InferenceId,
                FeedbackId = review.FeedbackId,
                ExpectedOutput = expectedOutput,
                CreatedAt = DateTimeOffset.UtcNow,
                Input = inference.Incident
            };
            await _trainingExampleCandidateRepository.SaveAsync(candidate, cancellationToken);
        }

        public async Task<IEnumerable<FeedbackReviewItem>> PendingReviews(CancellationToken cancellationToken = default)
        {
            var reviews = await _feedbackReviewRepository.GetPendingAsync(cancellationToken);

            if (reviews is null)
                return new List<FeedbackReviewItem>();

           return reviews;
        }

        public async Task RejectAsync(string reviewId, string reviewerComment, CancellationToken cancellationToken = default)
        {
            var review = await _feedbackReviewRepository.GetAsync(reviewId, cancellationToken);

            if (review is null)
                throw new InvalidOperationException("Review item not found.");

            var inference = await _inferenceRecordRepository.GetAsync(review.InferenceId, cancellationToken);

            if (inference is null)
                throw new InvalidOperationException("Inference record not found.");

            var feedbackItems = await _feedbackRepository.GetByInferenceIdAsync(review.InferenceId, cancellationToken);

            var feedback = feedbackItems.FirstOrDefault(f => f.FeedbackId == review.FeedbackId);

            if (feedback.CorrectCategory is null || feedback.CorrectRetryable is null
                || feedback.CorrectAction is null)
            {
                throw new InvalidOperationException("Approved feedback must contain a complete corrected answer");
            }

            review.Status = ReviewStatus.Rejected;
            review.ReviewerComment = reviewerComment;
            review.ReviewAt = DateTimeOffset.UtcNow;

            await _feedbackReviewRepository.SaveAsync(review, cancellationToken);
            _inferenceMetrics.RecordReviewOutcome(review.InferenceId, ReviewStatus.Rejected);
        }
    }
}
