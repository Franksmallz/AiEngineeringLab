using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IInferenceFeedbackRepository _inferenceFeedbackRepository;
        private readonly IInferenceRecordRepository _inferenceRecordRepository;
        private readonly IFeedbackReviewRepository _feedbackReviewRepository;
        private readonly IInferenceMetrics       _inferenceMetrics;

        public FeedbackService(IInferenceFeedbackRepository inferenceFeedbackRepository,
            IInferenceRecordRepository inferenceRecordRepository,
            IFeedbackReviewRepository feedbackReviewRepository,
            IInferenceMetrics inferenceMetrics)
        {
            _inferenceFeedbackRepository = inferenceFeedbackRepository;
            _inferenceRecordRepository = inferenceRecordRepository;
            _feedbackReviewRepository = feedbackReviewRepository;
            _inferenceMetrics = inferenceMetrics;
        }

        public async Task<string> SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default)
        {
            var inference = await _inferenceRecordRepository.GetAsync(request.InferenceId, cancellationToken);

            if(inference == null)
            {
                throw new InvalidOperationException($"Inference with ID {request.InferenceId}was not found.");
            }

            var feedback = new InferenceFeedback
            {
                FeedbackId = Guid.NewGuid().ToString(),
                InferenceId = request.InferenceId,
                IsCorrect = request.IsCorrect,
                CorrectCategory = request.CorrectCategory,
                CorrectRetryable = request.CorrectRetryable,
                CorrectAction = request.CorrectAction,
                Comment = request.Comment,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _inferenceFeedbackRepository.SaveAsync(feedback, cancellationToken);
            _inferenceMetrics.RecordFeedback(inference.DeploymentId, feedback.IsCorrect);

            if (!feedback.IsCorrect)
            {
                var reviewItem = new FeedbackReviewItem
                {
                    ReviewId = Guid.NewGuid().ToString(),
                    FeedbackId = feedback.FeedbackId,
                    InferenceId = feedback.InferenceId,
                    Status = ReviewStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                await _feedbackReviewRepository.SaveAsync(reviewItem);
            }
            return feedback.FeedbackId;
        }
    }
}
