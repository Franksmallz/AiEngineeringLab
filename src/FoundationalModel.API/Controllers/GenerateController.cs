using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FoundationalModel.API.Controllers;

[Route("api")]
[ApiController]
public class GenerateController : ControllerBase
{
    private readonly IGenerateService _generateService;
    private readonly IRagEvaluationService? _ragEvaluationService;

    public GenerateController(IGenerateService generateService, IRagEvaluationService? ragEvaluationService = null)
    {
        _generateService = generateService;
        _ragEvaluationService = ragEvaluationService;
    }

    [HttpPost("/generate")]
    [Produces(typeof(SendMessageResponseDto))]
    public async Task<IActionResult> Generate(SendMessageRequestDto model)
    {
        if (model is null || string.IsNullOrWhiteSpace(model.Prompt))
            return BadRequest(new { error = "Prompt is required." });

        return Ok(await _generateService.SendMessage(model));
    }

    [HttpPost("/generate-with-rag")]
    [Produces(typeof(RagCombinedResult))]
    public async Task<IActionResult> GenerateWithRag(RagEvaluationRequest model, CancellationToken cancellationToken)
    {
        if (model is null || string.IsNullOrWhiteSpace(model.Question) ||
            string.IsNullOrWhiteSpace(model.ExpectedAnswer) ||
            string.IsNullOrWhiteSpace(model.ExpectedSource) ||
            model.MatchingType == MatchingType.None)
        {
            return BadRequest(new { error = "Question, expectedAnswer, expectedSource, and a non-zero matchingType are required." });
        }

        if (_ragEvaluationService is null)
            throw new InvalidOperationException("RAG evaluation service is not configured.");

        return Ok(await _ragEvaluationService.EvaluateAsync(model, cancellationToken));
    }
}
