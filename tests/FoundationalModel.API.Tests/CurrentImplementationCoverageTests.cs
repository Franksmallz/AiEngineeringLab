using System.Net;
using System.Text.Json;
using FoundationalModel.API.Controllers;
using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Implementations;
using FoundationalModel.Repository.Interfaces;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FoundationalModel.API.Tests;

public sealed class CurrentImplementationCoverageTests
{
    [Fact]
    public async Task Inference_controller_rejects_missing_incident_and_delegates_valid_request()
    {
        var service = new StubInferenceService();
        var controller = new InferenceController(service);

        var bad = await controller.Generate(new PaymentIncidentRequest { Incident = " " });
        var good = await controller.Generate(new PaymentIncidentRequest { Incident = "Payment failed" });

        Assert.IsType<BadRequestObjectResult>(bad);
        Assert.IsType<OkObjectResult>(good);
        Assert.Equal("Payment failed", service.LastRequest?.Incident);
    }

    [Fact]
    public async Task Feedback_controller_delegates_request_and_returns_ok()
    {
        var service = new StubFeedbackService();
        var controller = new FeedbackController(service);
        var request = new SubmitFeedbackRequest { InferenceId = "inf-1", IsCorrect = true };

        var result = await controller.SubmitFeedback(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("feedback-1", ((OkObjectResult)result).Value);
        Assert.Same(request, service.LastRequest);
    }

    [Fact]
    public async Task Reviews_controller_delegates_approve_reject_and_pending_operations()
    {
        var service = new StubReviewService();
        var controller = new ReviewsController(service);

        Assert.IsType<OkResult>(await controller.Approve("review-1", "approved", CancellationToken.None));
        Assert.IsType<OkResult>(await controller.Reject("review-2", "rejected", CancellationToken.None));
        var pending = await controller.Reviews("", "", CancellationToken.None);

        Assert.Equal(("review-1", "approved"), service.Approved);
        Assert.Equal(("review-2", "rejected"), service.Rejected);
        Assert.IsType<OkObjectResult>(pending);
    }

    [Fact]
    public async Task Datasets_controller_exports_requested_version()
    {
        var service = new StubDatasetManagement();
        var controller = new DatasetsController(service);

        var result = await controller.SubmitFeedback("v2", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("v2", service.Version);
        Assert.Equal("finetuning/data", service.OutputDirectory);
    }

    [Fact]
    public async Task Feedback_service_saves_correct_feedback_without_review()
    {
        var feedbackRepository = new CapturingFeedbackRepository();
        var reviewRepository = new CapturingReviewRepository();
        var metrics = new CapturingMetrics();
        var service = new FeedbackService(
            feedbackRepository,
            new InMemoryInferenceRecordRepository(new InferenceRecord
            {
                InferenceId = "inf-1", Incident = "Payment failed", RawOutput = "raw",
                Category = "Card", Retryable = false, Action = "Review", LatencyMs = 1,
                DeploymentId = "model", CreatedAt = DateTimeOffset.UtcNow
            }),
            reviewRepository,
            metrics);

        var id = await service.SubmitAsync(new SubmitFeedbackRequest { InferenceId = "inf-1", IsCorrect = true });

        Assert.Equal(id, feedbackRepository.Last?.FeedbackId);
        Assert.Null(reviewRepository.Last);
        Assert.Equal(("model", true), metrics.Feedback);
    }

    [Fact]
    public async Task Feedback_service_creates_pending_review_for_incorrect_feedback()
    {
        var feedbackRepository = new CapturingFeedbackRepository();
        var reviewRepository = new CapturingReviewRepository();
        var service = new FeedbackService(
            feedbackRepository,
            new InMemoryInferenceRecordRepository(Record("inf-1")),
            reviewRepository,
            new CapturingMetrics());

        await service.SubmitAsync(new SubmitFeedbackRequest
        {
            InferenceId = "inf-1", IsCorrect = false, CorrectCategory = "Card", CorrectRetryable = false, CorrectAction = "Review"
        });

        Assert.NotNull(reviewRepository.Last);
        Assert.Equal(ReviewStatus.Pending, reviewRepository.Last!.Status);
        Assert.Equal(feedbackRepository.Last!.FeedbackId, reviewRepository.Last.FeedbackId);
    }

    [Fact]
    public async Task Feedback_service_rejects_unknown_inference()
    {
        var service = new FeedbackService(
            new CapturingFeedbackRepository(),
            new InMemoryInferenceRecordRepository(null),
            new CapturingReviewRepository(),
            new CapturingMetrics());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(
            new SubmitFeedbackRequest { InferenceId = "missing", IsCorrect = true }));
    }

    [Fact]
    public async Task Review_service_approves_correction_and_creates_training_candidate()
    {
        var review = Review("review-1", "feedback-1", "inf-1");
        var reviewRepository = new CapturingReviewRepository(review);
        var candidateRepository = new CapturingCandidateRepository();
        var metrics = new CapturingMetrics();
        var service = new FeedbackReviewService(
            reviewRepository,
            new CapturingFeedbackRepository(new InferenceFeedback
            {
                FeedbackId = "feedback-1", InferenceId = "inf-1", IsCorrect = false,
                CorrectCategory = "Card", CorrectRetryable = true, CorrectAction = "Retry",
                Comment = "wrong", CreatedAt = DateTimeOffset.UtcNow
            }),
            new InMemoryInferenceRecordRepository(Record("inf-1")),
            candidateRepository,
            metrics);

        await service.ApproveAsync("review-1", "looks good");

        Assert.Equal(ReviewStatus.Approved, review.Status);
        Assert.Equal("looks good", review.ReviewerComment);
        Assert.Equal(ReviewStatus.Approved, metrics.ReviewStatus);
        Assert.Contains("Category: Card", candidateRepository.Last!.ExpectedOutput);
        Assert.Contains("Retryable: Yes", candidateRepository.Last.ExpectedOutput);
    }

    [Fact]
    public async Task Review_service_rejects_correction_without_complete_feedback()
    {
        var service = new FeedbackReviewService(
            new CapturingReviewRepository(Review("review-1", "feedback-1", "inf-1")),
            new CapturingFeedbackRepository(new InferenceFeedback
            {
                FeedbackId = "feedback-1", InferenceId = "inf-1", IsCorrect = false,
                CorrectCategory = null, CorrectRetryable = null, CorrectAction = null,
                Comment = null, CreatedAt = DateTimeOffset.UtcNow
            }),
            new InMemoryInferenceRecordRepository(Record("inf-1")),
            new CapturingCandidateRepository(),
            new CapturingMetrics());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync("review-1", "incomplete"));
    }

    [Fact]
    public void Training_data_parser_maps_structured_output_lines()
    {
        var result = new TrainingDataParser().Parse(new TrainingExample
        {
            Input = "Payment failed",
            Output = "Category: Card\r\nRetryable: No\r\nAction: Review"
        });

        Assert.Equal("Payment failed", result.Input);
        Assert.Equal("Card", result.Category);
        Assert.Equal("No", result.Retryable);
        Assert.Equal("Review", result.Action);
    }

    [Fact]
    public async Task Training_dataset_management_exports_jsonl_and_metadata()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dataset-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new TrainingDatasetManagement(
                new CapturingCandidateRepository(
                    new TrainingExampleCandidate
                    {
                        CandidateId = "candidate-1", InferenceId = "inf-1", FeedbackId = "feedback-1",
                        Input = "Payment failed", ExpectedOutput = "Category: Card", CreatedAt = DateTimeOffset.UtcNow
                    }),
                new StubPathResolver(outputDirectory));

            var version = await service.ExportAsync("v2", "ignored");
            var line = await File.ReadAllTextAsync(version.Outpath);
            var document = JsonDocument.Parse(line);

            Assert.Equal("v2", version.Version);
            Assert.Equal(1, version.ExampleCount);
            Assert.Equal("Payment failed", document.RootElement.GetProperty("input").GetString());
            Assert.Equal("Category: Card", document.RootElement.GetProperty("expected").GetString());
        }
        finally
        {
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, true);
        }
    }

    [Fact]
    public async Task Repository_implementations_round_trip_records_feedback_reviews_and_metrics()
    {
        var records = new InferenceRecordRepository();
        var record = Record("inf-1");
        await records.SaveAsync(record);
        Assert.Same(record, await records.GetAsync("inf-1"));

        var feedback = new InferenceFeedbackRepository();
        var item = new InferenceFeedback
        {
            FeedbackId = "feedback-1", InferenceId = "inf-1", IsCorrect = true,
            CorrectCategory = "Card", CorrectRetryable = false, CorrectAction = "Review",
            Comment = null, CreatedAt = DateTimeOffset.UtcNow
        };
        await feedback.SaveAsync(item);
        Assert.Single(await feedback.GetByInferenceIdAsync("inf-1"));

        var reviews = new FeedbackReviewRepository();
        var review = Review("review-1", "feedback-1", "inf-1");
        await reviews.SaveAsync(review);
        Assert.Single(await reviews.GetPendingAsync());

        var metrics = new InferenceMetrics();
        metrics.RecordInference("model", 20, true, true);
        metrics.RecordFeedback("model", false);
        metrics.RecordReviewOutcome("model", ReviewStatus.Approved);
        var snapshot = metrics.Get("model");
        Assert.Equal(1, snapshot.TotalInferences);
        Assert.Equal(1, snapshot.NegativeFeedback);
        Assert.Equal(1, snapshot.ApprovedCorrections);
    }

    private static InferenceRecord Record(string id) => new()
    {
        InferenceId = id, Incident = "Payment failed", RawOutput = "raw", Category = "Card",
        Retryable = false, Action = "Review", LatencyMs = 1, DeploymentId = "model", CreatedAt = DateTimeOffset.UtcNow
    };

    private static FeedbackReviewItem Review(string reviewId, string feedbackId, string inferenceId) => new()
    {
        ReviewId = reviewId, FeedbackId = feedbackId, InferenceId = inferenceId,
        Status = ReviewStatus.Pending, CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class StubInferenceService : IPaymentIncidentInferenceService
    {
        public PaymentIncidentRequest? LastRequest { get; private set; }
        public Task<FoundationalModel.Models.Dtos.Responses.PaymentIncidentResponse> Generate(PaymentIncidentRequest request)
        {
            LastRequest = request;
            return Task.FromResult(PaymentIncidentInferenceTestsResponse());
        }

        private static FoundationalModel.Models.Dtos.Responses.PaymentIncidentResponse PaymentIncidentInferenceTestsResponse() => new()
        {
            InferenceId = "inf-1", Category = "Card", Retryable = false, Action = "Review",
            Metadata = new() { ModelName = "model", ModelVersion = "1", Precision = "FP16", BatchSize = 1, MaxNewTokens = 80, LatencyMs = 1 }
        };
    }

    private sealed class StubFeedbackService : IFeedbackService
    {
        public SubmitFeedbackRequest? LastRequest { get; private set; }
        public Task<string> SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default)
        { LastRequest = request; return Task.FromResult("feedback-1"); }
    }

    private sealed class StubReviewService : IFeedbackReviewService
    {
        public (string, string)? Approved { get; private set; }
        public (string, string)? Rejected { get; private set; }
        public Task ApproveAsync(string reviewId, string reviewerComment, CancellationToken cancellationToken = default)
        { Approved = (reviewId, reviewerComment); return Task.CompletedTask; }
        public Task RejectAsync(string reviewId, string reviewerComment, CancellationToken cancellationToken = default)
        { Rejected = (reviewId, reviewerComment); return Task.CompletedTask; }
        public Task<IEnumerable<FeedbackReviewItem>> PendingReviews(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<FeedbackReviewItem>>([]);
    }

    private sealed class StubDatasetManagement : ITrainingDatasetManagement
    {
        public string? Version { get; private set; }
        public string? OutputDirectory { get; private set; }
        public Task<TrainingDataVersion> ExportAsync(string version, string outputDirectory)
        {
            Version = version; OutputDirectory = outputDirectory;
            return Task.FromResult(new TrainingDataVersion { DataVersionId = "v", Version = version, ExampleCount = 0, Outpath = outputDirectory, CreatedAt = DateTimeOffset.UtcNow });
        }
    }

    private sealed class CapturingFeedbackRepository : IInferenceFeedbackRepository
    {
        private readonly IReadOnlyCollection<InferenceFeedback> _items;
        public InferenceFeedback? Last { get; private set; }
        public CapturingFeedbackRepository(InferenceFeedback? item = null) => _items = item is null ? [] : [item];
        public Task SaveAsync(InferenceFeedback feedback, CancellationToken cancellationToken = default) { Last = feedback; return Task.CompletedTask; }
        public Task<IReadOnlyCollection<InferenceFeedback>> GetByInferenceIdAsync(string inferenceId, CancellationToken cancellationToken = default) => Task.FromResult(_items);
    }

    private sealed class CapturingReviewRepository : IFeedbackReviewRepository
    {
        private readonly FeedbackReviewItem? _item;
        public FeedbackReviewItem? Last { get; private set; }
        public CapturingReviewRepository(FeedbackReviewItem? item = null) => _item = item;
        public Task SaveAsync(FeedbackReviewItem item, CancellationToken cancellationToken = default) { Last = item; return Task.CompletedTask; }
        public Task<FeedbackReviewItem?> GetAsync(string reviewId, CancellationToken cancellationToken = default) => Task.FromResult(_item);
        public Task<IReadOnlyCollection<FeedbackReviewItem>> GetPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<FeedbackReviewItem>>(_item is null ? [] : [_item]);
    }

    private sealed class CapturingCandidateRepository : ITrainingExampleCandidateRepository
    {
        private readonly IReadOnlyCollection<TrainingExampleCandidate> _items;
        public TrainingExampleCandidate? Last { get; private set; }
        public CapturingCandidateRepository(params TrainingExampleCandidate[] items) => _items = items;
        public Task SaveAsync(TrainingExampleCandidate candidate, CancellationToken cancellationToken = default) { Last = candidate; return Task.CompletedTask; }
        public Task<IReadOnlyCollection<TrainingExampleCandidate>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items);
    }

    private sealed class InMemoryInferenceRecordRepository : IInferenceRecordRepository
    {
        private readonly InferenceRecord? _record;
        public InMemoryInferenceRecordRepository(InferenceRecord? record) => _record = record;
        public Task SaveAsync(InferenceRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<InferenceRecord?> GetAsync(string inferenceId, CancellationToken cancellationToken = default) => Task.FromResult(_record?.InferenceId == inferenceId ? _record : null);
    }

    private sealed class CapturingMetrics : IInferenceMetrics
    {
        public (string DeploymentId, bool IsCorrect)? Feedback { get; private set; }
        public ReviewStatus? ReviewStatus { get; private set; }
        public void RecordInference(string deploymentId, long latencyMs, bool success, bool structuredOutputValid) { }
        public void RecordFeedback(string deploymentId, bool isCorrect) => Feedback = (deploymentId, isCorrect);
        public void RecordReviewOutcome(string deploymentId, ReviewStatus status) => ReviewStatus = status;
        public DeploymentMetrics? Get(string deploymentId) => null;
    }

    private sealed class StubPathResolver : IPathResolver
    {
        private readonly string _path;
        public StubPathResolver(string path) => _path = path;
        public string ResolveConfiguredPath(string configuredPath) => _path;
    }
}
