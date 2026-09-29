using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FoundationalModel.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InferenceController : ControllerBase
    {
        private readonly IPaymentIncidentInferenceService _inferenceService;

        public InferenceController(IPaymentIncidentInferenceService inferenceService)
        {
            _inferenceService = inferenceService;
        }

        [HttpPost]
        [Produces(typeof(PaymentIncidentResponse))]
        public async Task<IActionResult> Generate(PaymentIncidentRequest model)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Incident))
                return BadRequest(new { error = "Incident description is required." });

            return Ok(await _inferenceService.Generate(model));
        }
    }
}
