using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RiskInference.Api.Contracts;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

// Chapter 7, Step 7: the service must be observable and must behave in a
// predictable way when something goes wrong. Every test starts the real application.
public class OperationsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CapturedLog CompletedLog(OperationsTestHost host, string requestId) =>
        host.Logs.Single(l => l.Message.StartsWith("Request completed") && (string?)l.State["RequestId"] == requestId);

    private static string EverythingThatWasLogged(OperationsTestHost host) =>
        string.Join("\n", host.Logs.Select(l =>
            l.Message + " " + string.Join(" ", l.State.Select(kv => $"{kv.Key}={kv.Value}"))));

    // ===================== Health: live and ready =====================

    [Fact]
    public async Task Live_and_ready_are_both_200_for_a_healthy_service()
    {
        await using var host = await OperationsTestHost.StartAsync();

        var live = await host.GetJsonAsync("/health/live");
        var ready = await host.GetJsonAsync("/health/ready");

        Assert.Equal("Alive", live.GetProperty("status").GetString());
        Assert.Equal("Ready", ready.GetProperty("status").GetString());
        Assert.Equal("chapter-06-v1", ready.GetProperty("modelVersion").GetString());
        Assert.Equal("risk-policy-v1", ready.GetProperty("decisionPolicyVersion").GetString());
    }

    // The model or the manifest changes on disk after the start. Readiness must fail.
    // Liveness must not fail: a restart would not repair a bad file.
    [Theory]
    [InlineData("model_unavailable")]
    [InlineData("model_hash_mismatch")]
    [InlineData("model_invalid")]
    [InlineData("manifest_unavailable")]
    [InlineData("manifest_invalid")]
    [InlineData("manifest_changed")]
    public async Task A_bad_artifact_after_start_fails_readiness_but_not_liveness_and_recovers(string reason)
    {
        using var artifact = new TempArtifact();
        await using var host = await OperationsTestHost.StartAsync(artifact.ManifestFile);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/ready")).StatusCode);

        // Damage the artifact.
        switch (reason)
        {
            case "model_unavailable": File.Delete(artifact.ModelFile); break;
            case "model_hash_mismatch": ArtifactTamper.ChangeOneByteInTheLargestEntry(artifact.ModelFile); break;
            case "model_invalid": File.WriteAllText(artifact.ModelFile, "this is not a zip file"); break;
            case "manifest_unavailable": File.Delete(artifact.ManifestFile); break;
            case "manifest_invalid": File.WriteAllText(artifact.ManifestFile, "{ not json"); break;
            case "manifest_changed": artifact.WriteManifest(m => m["threshold"] = 0.5); break;
        }
        await host.Health.CheckNowAsync();

        // Readiness fails with the reason code and nothing else.
        using var ready = await host.Client.GetAsync("/health/ready");
        var readyText = await ready.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("NotReady", JsonDocument.Parse(readyText).RootElement.GetProperty("status").GetString());
        Assert.Equal(reason, JsonDocument.Parse(readyText).RootElement.GetProperty("reason").GetString());
        Assert.DoesNotContain(artifact.Directory, readyText);

        // Liveness still passes.
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/live")).StatusCode);

        // The service refuses predictions, with a clear and safe answer. It does not hide the problem behind a 200.
        using var predict = await host.PostAsync(OperationsTestHost.ValidBody);
        var predictText = await predict.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, predict.StatusCode);
        Assert.Contains(reason, predictText);
        Assert.Contains("requestId", predictText);
        Assert.DoesNotContain(artifact.Directory, predictText);

        // The operator gets a log line with the reason and the model version.
        var lost = host.Logs.Single(l => l.Message.StartsWith("Readiness lost"));
        Assert.Equal(LogLevel.Error, lost.Level);
        Assert.Equal(reason, lost.State["Reason"]);
        Assert.Equal("test-v1", lost.State["ModelVersion"]);
        Assert.DoesNotContain(artifact.Directory, lost.Message);

        // The metrics count the refusal as a server-side problem.
        var metrics = await host.GetJsonAsync("/metrics");
        Assert.Equal(1, metrics.GetProperty("requestsByOutcome").GetProperty("NotReady").GetInt32());
        Assert.True(metrics.GetProperty("serverErrorRate").GetDouble() > 0);

        // The artifact is repaired. The service becomes ready again with no restart.
        artifact.Restore();
        await host.Health.CheckNowAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync(OperationsTestHost.ValidBody)).StatusCode);
        Assert.Contains(host.Logs, l => l.Message.StartsWith("Readiness restored"));
    }

    [Fact]
    public async Task The_background_monitor_finds_a_bad_model_without_any_request()
    {
        using var artifact = new TempArtifact();
        await using var host = await OperationsTestHost.StartAsync(artifact.ManifestFile, integrityCheckSeconds: 1);

        File.Delete(artifact.ModelFile);
        await WaitUntilAsync(async () =>
            (await host.Client.GetAsync("/health/ready")).StatusCode == HttpStatusCode.ServiceUnavailable);

        artifact.Restore();
        await WaitUntilAsync(async () =>
            (await host.Client.GetAsync("/health/ready")).StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_fails_when_the_host_begins_to_stop()
    {
        await using var host = await OperationsTestHost.StartAsync();
        Assert.True(host.Health.Current.Ready);

        host.App.Lifetime.StopApplication();

        Assert.False(host.Health.Current.Ready);
        Assert.Equal("shutting_down", host.Health.Current.Reason);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int seconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail($"The condition did not become true in {seconds} s.");
    }

    // ===================== Scenario 1 and 2: bad artifact at start =====================

    [Theory]
    [InlineData("missing_model")]
    [InlineData("corrupted_model")]
    [InlineData("wrong_hash")]
    [InlineData("invalid_threshold")]
    [InlineData("invalid_manifest_json")]
    [InlineData("unsupported_policy_version")]
    [InlineData("missing_manifest")]
    public void A_bad_artifact_stops_the_start_up_so_the_service_is_never_ready(string problem)
    {
        using var artifact = new TempArtifact();
        switch (problem)
        {
            case "missing_model": File.Delete(artifact.ModelFile); break;
            case "corrupted_model": File.WriteAllText(artifact.ModelFile, "this is not a zip file"); break;
            case "wrong_hash": artifact.WriteManifest(m => m["modelSha256"] = new string('a', 64)); break;
            case "invalid_threshold": artifact.WriteManifest(m => m["threshold"] = 1.5); break;
            case "invalid_manifest_json": File.WriteAllText(artifact.ManifestFile, "{ not json"); break;
            case "unsupported_policy_version": artifact.WriteManifest(m => m["decisionPolicyVersion"] = "risk-policy-v9"); break;
            case "missing_manifest": File.Delete(artifact.ManifestFile); break;
        }

        // The host is never built, so it can never listen or answer a probe.
        var ex = Assert.Throws<InvalidOperationException>(() => RiskApiHost.Build(
            ["--urls", "http://127.0.0.1:0", $"--RiskModel:ManifestPath={artifact.ManifestFile}"]));
        Assert.Contains("could not be loaded", ex.Message);
    }

    // ===================== Scenario 3: invalid prediction requests =====================

    [Fact]
    public async Task An_invalid_request_gets_400_with_the_request_id_and_one_log_line_with_no_input_values()
    {
        await using var host = await OperationsTestHost.StartAsync();

        using var response = await host.PostAsync(
            """{ "amount": -777.5, "transactionHour": 24, "isHighRiskCountry": true }""", requestId: "req-invalid-1");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("req-invalid-1", response.Headers.GetValues("X-Request-ID").Single());
        Assert.Equal("req-invalid-1", JsonDocument.Parse(text).RootElement.GetProperty("requestId").GetString());

        var log = CompletedLog(host, "req-invalid-1");
        Assert.Equal("ValidationFailed", log.State["Outcome"]);
        Assert.Equal(400, log.State["StatusCode"]);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.DoesNotContain("777.5", EverythingThatWasLogged(host));
        Assert.DoesNotContain("777.5", text);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("""{ "amount": "lots" }""")]
    public async Task A_malformed_request_gets_400_with_a_request_id_and_a_clear_outcome_in_the_log(string body)
    {
        await using var host = await OperationsTestHost.StartAsync();

        using var response = await host.PostAsync(body, requestId: "req-malformed-1");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("req-malformed-1", JsonDocument.Parse(text).RootElement.GetProperty("requestId").GetString());
        Assert.Equal("MalformedRequest", CompletedLog(host, "req-malformed-1").State["Outcome"]);
        Assert.DoesNotContain("Exception", text);
    }

    // ===================== Structured logging =====================

    [Fact]
    public async Task A_prediction_writes_one_structured_log_line_with_the_required_fields_and_no_transaction_data()
    {
        await using var host = await OperationsTestHost.StartAsync();
        const string body = """
            { "amount": 98765.4321, "transactionHour": 2, "customerTransactionCount24h": 12,
              "recentFailureCount": 3, "beneficiaryAgeDays": 2, "isHighRiskCountry": true }
            """;

        using var response = await host.PostAsync(body, requestId: "req-abc.123");
        var prediction = (await response.Content.ReadFromJsonAsync<RiskPredictionResponse>(Web))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("req-abc.123", response.Headers.GetValues("X-Request-ID").Single());

        var log = CompletedLog(host, "req-abc.123");
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal("chapter-06-v1", log.State["ModelVersion"]);
        Assert.Equal("risk-policy-v1", log.State["PolicyVersion"]);
        Assert.Equal("Predicted", log.State["Outcome"]);
        Assert.Equal(200, log.State["StatusCode"]);
        Assert.Equal("POST", log.State["Method"]);
        Assert.Equal("/risk/predict", log.State["Route"]);
        Assert.True((double)log.State["DurationMs"]! > 0);
        Assert.Equal(prediction.PredictedRisk, log.State["PredictedRisk"]);
        Assert.Equal(prediction.FlaggedHighRisk, log.State["Flagged"]);

        // No transaction field, no value from the request, and no score, in any log line.
        var everything = EverythingThatWasLogged(host);
        Assert.DoesNotContain("98765", everything);
        Assert.DoesNotContain("customerTransactionCount24h", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beneficiaryAge", everything, StringComparison.OrdinalIgnoreCase);
        // The score is a model output for one transaction. It is returned to the caller, not logged.
        Assert.DoesNotContain("HighRiskScore", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(host.Logs.SelectMany(l => l.State.Keys), key => key.Contains("Score", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_request_without_an_id_gets_a_new_id_that_is_in_the_header_and_the_log()
    {
        await using var host = await OperationsTestHost.StartAsync();

        using var response = await host.PostAsync(OperationsTestHost.ValidBody);
        var id = response.Headers.GetValues("X-Request-ID").Single();

        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.Equal("Predicted", CompletedLog(host, id).State["Outcome"]);
    }

    [Theory]
    [InlineData("has spaces in it")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task An_unsafe_request_id_from_the_caller_is_replaced(string unsafeId)
    {
        await using var host = await OperationsTestHost.StartAsync();

        using var response = await host.PostAsync(OperationsTestHost.ValidBody, requestId: unsafeId);
        var id = response.Headers.GetValues("X-Request-ID").Single();

        Assert.NotEqual(unsafeId, id);
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.DoesNotContain(unsafeId, EverythingThatWasLogged(host));
    }

    [Fact]
    public async Task Probe_and_metrics_requests_are_not_counted_and_not_logged_at_information_level()
    {
        await using var host = await OperationsTestHost.StartAsync();

        await host.Client.GetAsync("/health/live");
        await host.Client.GetAsync("/health/ready");
        await host.Client.GetAsync("/metrics");

        var metrics = await host.GetJsonAsync("/metrics");
        Assert.Equal(0, metrics.GetProperty("requests").GetInt32());
        Assert.DoesNotContain(host.Logs,
            l => l.Message.StartsWith("Request completed") && l.Level >= LogLevel.Information);
    }

    // ===================== Scenario 6: a controlled inference exception =====================

    [Fact]
    public async Task An_inference_exception_gives_a_sanitized_500_and_a_full_log_and_the_service_keeps_working()
    {
        await using var host = await OperationsTestHost.StartAsync(
            configure: b => ServiceReplacement.Wrap(b, real => new FaultInjectingModelService(real)));
        var faulty = OperationsTestHost.ValidBody.Replace("4500", FaultInjectingModelService.FaultyAmount.ToString("0.00###"));

        using var response = await host.PostAsync(faulty, requestId: "req-boom-1");
        var text = await response.Content.ReadAsStringAsync();

        // The client gets a fixed message and the request ID. Nothing else.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("An unexpected error occurred.", body.GetProperty("title").GetString());
        Assert.Equal("req-boom-1", body.GetProperty("requestId").GetString());
        Assert.Equal("req-boom-1", response.Headers.GetValues("X-Request-ID").Single());
        foreach (var secret in new[] { "secret-internal-detail", "C:\\", "risk.zip", "InvalidOperationException", "   at ", "StackTrace" })
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);

        // The log has the exception, the request ID and the versions.
        var error = host.Logs.Single(l => l.Level == LogLevel.Error && l.Message.StartsWith("Unexpected error"));
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Contains("secret-internal-detail", error.Exception!.Message);
        Assert.Equal("req-boom-1", error.State["RequestId"]);
        Assert.Equal("chapter-06-v1", error.State["ModelVersion"]);
        Assert.Equal("risk-policy-v1", error.State["PolicyVersion"]);

        var completed = CompletedLog(host, "req-boom-1");
        Assert.Equal(LogLevel.Error, completed.Level);
        Assert.Equal("Error", completed.State["Outcome"]);
        Assert.Equal(500, completed.State["StatusCode"]);

        // The metrics count it as a server error.
        var metrics = await host.GetJsonAsync("/metrics");
        Assert.Equal(1, metrics.GetProperty("requestsByOutcome").GetProperty("Error").GetInt32());
        Assert.Equal(1.0, metrics.GetProperty("serverErrorRate").GetDouble());

        // One bad request does not break the service. It is still ready and still correct.
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/ready")).StatusCode);
        using var next = await host.PostAsync(OperationsTestHost.ValidBody);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    // The same, for the real service: an exception inside the engine must not break the pool.
    [Theory]
    [InlineData(InferenceMode.Pooled)]
    [InlineData(InferenceMode.EnginePerRequest)]
    public void An_exception_inside_the_engine_does_not_break_later_predictions(InferenceMode mode)
    {
        using var service = TestSupport.CreateService(mode);
        var input = new DatasetSample().First();
        var expected = service.Predict(input);

        for (var i = 0; i < 50; i++)
        {
            Assert.ThrowsAny<Exception>(() => service.Predict(null!));
            Assert.Equal(expected, service.Predict(input));
        }

        Assert.Null(service.VerifyIntegrity());
    }

    // ===================== Metrics =====================

    [Fact]
    public async Task Metrics_count_requests_outcomes_predictions_and_the_flag_rate_correctly()
    {
        await using var host = await OperationsTestHost.StartAsync();
        var samples = new DatasetSample().Take(60).ToList();

        var predictions = new List<RiskPredictionResponse>();
        foreach (var s in samples)
        {
            var body = JsonSerializer.Serialize(new
            {
                amount = s.Amount, transactionHour = s.TransactionHour,
                customerTransactionCount24h = s.CustomerTransactionCount24h,
                recentFailureCount = s.RecentFailureCount, beneficiaryAgeDays = s.BeneficiaryAgeDays,
                isHighRiskCountry = s.IsHighRiskCountry
            });
            using var response = await host.PostAsync(body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            predictions.Add((await response.Content.ReadFromJsonAsync<RiskPredictionResponse>(Web))!);
        }
        for (var i = 0; i < 5; i++) await host.PostAsync("""{ "amount": -1 }""");
        for (var i = 0; i < 3; i++) await host.PostAsync("{ nope");

        var m = await host.GetJsonAsync("/metrics");
        var byOutcome = m.GetProperty("requestsByOutcome");

        Assert.Equal(68, m.GetProperty("requests").GetInt64());
        Assert.Equal(60, byOutcome.GetProperty("Predicted").GetInt64());
        Assert.Equal(5, byOutcome.GetProperty("ValidationFailed").GetInt64());
        Assert.Equal(3, byOutcome.GetProperty("MalformedRequest").GetInt64());
        Assert.Equal(0, byOutcome.GetProperty("Error").GetInt64());
        Assert.Equal(0.0, m.GetProperty("serverErrorRate").GetDouble());
        Assert.Equal(8.0 / 68, m.GetProperty("clientErrorRate").GetDouble(), precision: 5);

        // The distribution of predictions and the flag rate equal what the responses said.
        Assert.Equal(60, m.GetProperty("predictions").GetInt64());
        var byRisk = m.GetProperty("predictionsByRiskLevel");
        foreach (var level in new[] { "Low", "Medium", "High" })
            Assert.Equal(predictions.Count(p => p.PredictedRisk == level), byRisk.GetProperty(level).GetInt64());
        var flagged = predictions.Count(p => p.FlaggedHighRisk);
        Assert.Equal(flagged, m.GetProperty("flaggedHighRisk").GetInt64());
        Assert.Equal((double)flagged / 60, m.GetProperty("highRiskFlagRate").GetDouble(), precision: 5);

        // Latency: the percentiles are in order and the count is the request count.
        var latency = m.GetProperty("latency");
        Assert.Equal(68, latency.GetProperty("count").GetInt64());
        var (p50, p95, p99, max) = (latency.GetProperty("p50Ms").GetDouble(), latency.GetProperty("p95Ms").GetDouble(),
            latency.GetProperty("p99Ms").GetDouble(), latency.GetProperty("maxMs").GetDouble());
        Assert.InRange(p50, 0.001, 5000);
        Assert.True(p50 <= p95 && p95 <= p99 && p99 <= max, $"p50 {p50} p95 {p95} p99 {p99} max {max}");

        Assert.Equal("chapter-06-v1", m.GetProperty("modelVersion").GetString());
        Assert.Equal("risk-policy-v1", m.GetProperty("decisionPolicyVersion").GetString());

        // The metrics hold counts only. They contain nothing from the transactions.
        var text = m.GetRawText();
        Assert.DoesNotContain("amount", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_latency_percentiles_are_within_10_percent_of_the_exact_values()
    {
        using var metrics = new Operations.RiskMetrics();
        var random = new Random(7);
        var values = Enumerable.Range(0, 20_000).Select(_ => Math.Exp(random.NextDouble() * 6 - 3)).ToArray();   // 0.05 to 20 ms
        foreach (var v in values) metrics.Record(Operations.RequestOutcome.Predicted, v, "Low");

        var sorted = values.OrderBy(v => v).ToArray();
        var snapshot = metrics.Snapshot("m", "p").Latency;

        double Exact(double p) => sorted[(int)Math.Ceiling(p * sorted.Length) - 1];
        Assert.InRange(snapshot.P50Ms / Exact(0.50), 1.0, 1.10 + 1e-9);
        Assert.InRange(snapshot.P95Ms / Exact(0.95), 1.0, 1.10 + 1e-9);
        Assert.InRange(snapshot.P99Ms / Exact(0.99), 1.0, 1.10 + 1e-9);
    }

    // ===================== Scenario 4: concurrent traffic during shutdown =====================

    [Fact]
    public async Task Concurrent_traffic_during_shutdown_gets_only_complete_correct_answers_or_a_clean_refusal()
    {
        await using var host = await OperationsTestHost.StartAsync(
            configure: b => ServiceReplacement.Wrap(b, real => new DelayingModelService(real, TimeSpan.FromMilliseconds(40))),
            shutdownSeconds: 20);

        var ok = 0;
        var wrongStatus = new System.Collections.Concurrent.ConcurrentBag<HttpStatusCode>();
        var cutAnswers = new System.Collections.Concurrent.ConcurrentBag<string>();
        var refused = 0;
        var stopRequested = false;

        async Task Worker()
        {
            using var client = new HttpClient { BaseAddress = host.Client.BaseAddress, Timeout = TimeSpan.FromSeconds(30) };
            var until = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < until)
            {
                try
                {
                    using var response = await client.PostAsync("/risk/predict",
                        new StringContent(OperationsTestHost.ValidBody, Encoding.UTF8, "application/json"));
                    if (response.StatusCode != HttpStatusCode.OK) { wrongStatus.Add(response.StatusCode); continue; }
                    var body = await response.Content.ReadFromJsonAsync<RiskPredictionResponse>(Web);
                    if (body?.ModelVersion != "chapter-06-v1") cutAnswers.Add("bad body");
                    Interlocked.Increment(ref ok);
                }
                catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ResponseEnded)
                {
                    cutAnswers.Add(ex.Message);   // an answer that ended in the middle
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref refused);   // no connection: the host no longer listens
                    if (Volatile.Read(ref stopRequested)) return;
                }
                catch (JsonException ex) { cutAnswers.Add(ex.Message); }
            }
        }

        var workers = Enumerable.Range(0, 30).Select(_ => Task.Run(Worker)).ToArray();
        await Task.Delay(600);
        Volatile.Write(ref stopRequested, true);
        var started = DateTime.UtcNow;
        await host.StopAsync();
        var stopSeconds = (DateTime.UtcNow - started).TotalSeconds;
        await Task.WhenAll(workers);

        Assert.True(ok > 100, $"Only {ok} requests finished.");
        Assert.Empty(wrongStatus);   // no 5xx, no 503: the host does not answer with errors while it stops
        Assert.Empty(cutAnswers);    // no answer was cut in the middle
        Assert.True(stopSeconds < 10, $"The stop took {stopSeconds:F1} s.");
    }

    // ===================== Scenario 5: restart with an unchanged model =====================

    [Fact]
    public async Task A_restart_with_an_unchanged_model_gives_the_same_versions_and_the_same_predictions()
    {
        var samples = new DatasetSample().Take(50).ToList();

        async Task<(string Ready, List<string> Answers)> RunOnce()
        {
            await using var host = await OperationsTestHost.StartAsync();
            var ready = (await host.GetJsonAsync("/health/ready")).GetRawText();
            var answers = new List<string>();
            foreach (var s in samples)
            {
                var body = JsonSerializer.Serialize(new
                {
                    amount = s.Amount, transactionHour = s.TransactionHour,
                    customerTransactionCount24h = s.CustomerTransactionCount24h,
                    recentFailureCount = s.RecentFailureCount, beneficiaryAgeDays = s.BeneficiaryAgeDays,
                    isHighRiskCountry = s.IsHighRiskCountry
                });
                using var response = await host.PostAsync(body);
                answers.Add(await response.Content.ReadAsStringAsync());
            }
            return (ready, answers);
        }

        var first = await RunOnce();
        var second = await RunOnce();

        // The readiness body has a time stamp. Compare it without that field.
        static string WithoutTime(string json) => Regex.Replace(json, "\"checkedAt\":\"[^\"]*\"", "");
        Assert.Equal(WithoutTime(first.Ready), WithoutTime(second.Ready));
        Assert.Equal(first.Answers, second.Answers);
    }
}
