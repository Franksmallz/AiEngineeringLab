using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Contracts;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

// Test doubles that wrap the real model service.
internal sealed class DelayingModelService(IRiskModelService inner, TimeSpan delay) : IRiskModelService
{
    public ActiveModelInfo Active => inner.Active;
    public string? VerifyIntegrity() => inner.VerifyIntegrity();

    public RiskPredictionResponse Predict(TransactionRiskData input)
    {
        Thread.Sleep(delay);
        return inner.Predict(input);
    }
}

// Throws for one amount. The message holds fake internal detail, so a test can
// check that this detail never reaches the client.
internal sealed class FaultInjectingModelService(IRiskModelService inner) : IRiskModelService
{
    public const float FaultyAmount = 13.13f;
    public const string SecretDetail = "secret-internal-detail C:\\models\\private\\risk.zip";

    public ActiveModelInfo Active => inner.Active;
    public string? VerifyIntegrity() => inner.VerifyIntegrity();

    public RiskPredictionResponse Predict(TransactionRiskData input) =>
        input.Amount == FaultyAmount
            ? throw new InvalidOperationException(SecretDetail)
            : inner.Predict(input);
}

internal static class ServiceReplacement
{
    // Replaces IRiskModelService with a wrapper around a real RiskModelService.
    public static void Wrap(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder,
        Func<IRiskModelService, IRiskModelService> wrap) =>
        builder.Services.AddSingleton<IRiskModelService>(sp => wrap(new RiskModelService(
            sp.GetRequiredService<IOptions<RiskModelOptions>>(), sp.GetRequiredService<IPathResolver>())));
}
