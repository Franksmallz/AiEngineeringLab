using FoundationalModel.API.Controllers;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Models.Configs;
using FoundationalModel.Services.Implementations.Providers;
using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FoundationalModel.API.Tests;

public sealed class GenerateControllerTests
{
    [Fact]
    public async Task Generate_returns_ok_with_the_service_response()
    {
        var request = new SendMessageRequestDto { Prompt = "Explain dependency injection." };
        var expected = new SendMessageResponseDto
        {
            Model = "ClaudeHaiku4_5",
            Text = "Dependency injection supplies dependencies to a class.",
            Success = true,
            InputTokens = 8,
            OutputTokens = 10
        };
        var service = new StubGenerateService(expected);
        var controller = new GenerateController(service);

        var result = await controller.Generate(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.Same(request, service.ReceivedRequest);
    }

    [Fact]
    public async Task Generate_returns_provider_failure_response_without_changing_it()
    {
        var expected = new SendMessageResponseDto
        {
            Success = false,
            ErrorMessage = "The model provider is unavailable."
        };
        var service = new StubGenerateService(expected);
        var controller = new GenerateController(service);

        var result = await controller.Generate(new SendMessageRequestDto { Prompt = "Try again." });

        var ok = Assert.IsType<OkObjectResult>(result);
        var actual = Assert.IsType<SendMessageResponseDto>(ok.Value);
        Assert.False(actual.Success);
        Assert.Equal(expected.ErrorMessage, actual.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Generate_rejects_an_empty_prompt(string? prompt)
    {
        var service = new StubGenerateService(new SendMessageResponseDto());
        var controller = new GenerateController(service);

        var result = await controller.Generate(new SendMessageRequestDto { Prompt = prompt! });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = badRequest.Value!
            .GetType()
            .GetProperty("error")!
            .GetValue(badRequest.Value)
            ?.ToString();
        Assert.Equal("Prompt is required.", error);
        Assert.Null(service.ReceivedRequest);
    }

    [Fact]
    public void Response_mapper_normalizes_usage_metadata_and_cost()
    {
        var response = AnthropicResponseMapper.CreateSuccess(
            "claude-haiku-4-5",
            "Hello",
            inputTokens: 1_000,
            outputTokens: 500,
            latencyMs: 125,
            pricing: new ModelCost { InputPerMillion = 1m, OutputPerMillion = 5m },
            temperature: 1,
            prompt: "Define idempotency");

        Assert.True(response.Success);
        Assert.Equal("claude-haiku-4-5", response.Model);
        Assert.Equal("Hello", response.Text);
        Assert.Equal(1_000, response.InputTokens);
        Assert.Equal(500, response.OutputTokens);
        Assert.Equal(125, response.LatencyMs);
        Assert.Equal(0.0035m, response.EstimatedCost);
    }

    [Fact]
    public void Cost_calculation_returns_zero_when_pricing_is_missing()
    {
        var cost = AnthropicResponseMapper.CalculateCost(null, 100, 100);

        Assert.Equal(0m, cost);
    }

    private sealed class StubGenerateService(SendMessageResponseDto response) : IGenerateService
    {
        public SendMessageRequestDto? ReceivedRequest { get; private set; }

        public Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request)
        {
            ReceivedRequest = request;
            return Task.FromResult(response);
        }

        public Task<SendMessageResponseDto> SendMessageWithTools(SendMessageRequestDto request)
        {
            ReceivedRequest = request;
            return Task.FromResult(response);
        }
    }
}
