using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FoundationalModel.API.Controllers
{
    [Route("api")]
    [ApiController]
    public class GenerateController : ControllerBase
    {
        private readonly IGenerateService _generateService;

        public GenerateController(IGenerateService generateService)
        {
            _generateService = generateService;
        }

        [HttpPost("/generate")]
        [Produces(typeof(SendMessageResponseDto))]
        public async Task<IActionResult> Generate(SendMessageRequestDto model)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Prompt))
            {
                return BadRequest(new { error = "Prompt is required." });
            }

            var response = await _generateService.SendMessage(model);

            return Ok(response);
        }
    }
}
