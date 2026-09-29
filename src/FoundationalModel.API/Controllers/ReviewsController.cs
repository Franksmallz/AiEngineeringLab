using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FoundationalModel.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly IFeedbackReviewService _feedbackReviewService;

        public ReviewsController(IFeedbackReviewService feedbackReviewService)
        {
            _feedbackReviewService = feedbackReviewService;
        }

        [HttpPost("reviews/{reviewId}/{reviewComment}/approve")]
        public async Task<IActionResult> Approve(string reviewId, string reviewComment, CancellationToken cancellationToken)
        {
            await _feedbackReviewService.ApproveAsync(reviewId, reviewComment, cancellationToken);
            return Ok();
        }

        [HttpPost("reviews/{reviewId}/{reviewComment}/reject")]
        public async Task<IActionResult> Reject(string reviewId, string reviewComment, CancellationToken cancellationToken)
        {
            await _feedbackReviewService.RejectAsync(reviewId, reviewComment, cancellationToken);
            return Ok();
        }

        [HttpGet("reviews/pending")]
        public async Task<IActionResult> Reviews(string reviewId, string reviewComment, CancellationToken cancellationToken)
        {
            var pendingReviews = await _feedbackReviewService.PendingReviews(cancellationToken);
            return Ok(pendingReviews);
        }
    }
}
