using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Contracts;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

// These tests start the real application on a real port (Kestrel), then stop it.
public class GracefulShutdownTests
{
    private const string ValidJson = """
        { "amount": 4500, "transactionHour": 2, "customerTransactionCount24h": 12,
          "recentFailureCount": 3, "beneficiaryAgeDays": 2, "isHighRiskCountry": true }
        """;

    // The real service, made slow, so that requests are still in progress when the host stops.
    private sealed class SlowModelService : IRiskModelService
    {
        private readonly IRiskModelService _inner;
        private readonly TimeSpan _delay;

        public SlowModelService(IRiskModelService inner, TimeSpan delay) { _inner = inner; _delay = delay; }

        public ActiveModelInfo Active => _inner.Active;

        public string? VerifyIntegrity() => _inner.VerifyIntegrity();

        public RiskPredictionResponse Predict(TransactionRiskData input)
        {
            Thread.Sleep(_delay);
            return _inner.Predict(input);
        }
    }

    private static Microsoft.AspNetCore.Builder.WebApplication BuildHost(TimeSpan predictDelay, int shutdownSeconds) =>
        RiskApiHost.Build(
            [
                "--urls", "http://127.0.0.1:0",
                $"--RiskModel:ManifestPath={TestSupport.ManifestPath}",
                $"--RiskModel:ShutdownTimeoutSeconds={shutdownSeconds}",
                "--Logging:LogLevel:Default=Warning"
            ],
            builder => builder.Services.AddSingleton<IRiskModelService>(sp => new SlowModelService(
                new RiskModelService(sp.GetRequiredService<IOptions<RiskModelOptions>>(), sp.GetRequiredService<IPathResolver>()),
                predictDelay)));

    private static string AddressOf(Microsoft.AspNetCore.Builder.WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    [Fact]
    public void The_shutdown_timeout_comes_from_the_configuration()
    {
        var app = BuildHost(TimeSpan.Zero, shutdownSeconds: 7);

        Assert.Equal(TimeSpan.FromSeconds(7), app.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    [Fact]
    public async Task Requests_in_progress_finish_when_the_host_stops_and_new_requests_are_refused()
    {
        await using var app = BuildHost(predictDelay: TimeSpan.FromMilliseconds(800), shutdownSeconds: 20);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(AddressOf(app)), Timeout = TimeSpan.FromSeconds(30) };

        // 10 requests, each takes about 0.8 s inside the service.
        var inFlight = Enumerable.Range(0, 10)
            .Select(_ => client.PostAsync("/risk/predict",
                new StringContent(ValidJson, System.Text.Encoding.UTF8, "application/json")))
            .ToList();
        await Task.Delay(300);   // the requests are now being served

        var started = DateTime.UtcNow;
        await app.StopAsync();
        var stopTime = DateTime.UtcNow - started;

        // Every request that was in progress got a full, correct answer.
        foreach (var request in inFlight)
        {
            var response = await request;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = (await response.Content.ReadFromJsonAsync<RiskPredictionResponse>(
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
            Assert.Equal("chapter-06-v1", body.ModelVersion);
        }

        // The host waited for them (the work needed about 0.5 s more), and did not wait for the full timeout.
        Assert.InRange(stopTime.TotalSeconds, 0.2, 15);

        // After the stop, the service takes no new request.
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsync("/risk/predict",
            new StringContent(ValidJson, System.Text.Encoding.UTF8, "application/json")));
    }

    [Fact]
    public async Task A_host_with_no_requests_in_progress_stops_at_once()
    {
        await using var app = BuildHost(TimeSpan.Zero, shutdownSeconds: 20);
        await app.StartAsync();

        var started = DateTime.UtcNow;
        await app.StopAsync();

        Assert.True((DateTime.UtcNow - started).TotalSeconds < 5);
    }
}
