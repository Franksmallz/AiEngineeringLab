using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Implementations.Providers;

public static class AnthropicResponseMapper
{
    public static SendMessageResponseDto CreateSuccess(
        string model,
        string text,
        long inputTokens,
        long outputTokens,
        long latencyMs,
        ModelCost? pricing, 
        double temperature,
        string prompt)
    {
        return new SendMessageResponseDto
        {
            Model = model,
            Text = text,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            LatencyMs = latencyMs,
            EstimatedCost = CalculateCost(pricing, inputTokens, outputTokens),
            Success = true,
            Temperature = temperature,
            Prompt = prompt
        };
    }

    public static decimal CalculateCost(ModelCost? pricing, long inputTokens, long outputTokens)
    {
        if (pricing is null)
        {
            return 0m;
        }

        return (pricing.InputPerMillion * inputTokens + pricing.OutputPerMillion * outputTokens) / 1_000_000m;
    }
}
