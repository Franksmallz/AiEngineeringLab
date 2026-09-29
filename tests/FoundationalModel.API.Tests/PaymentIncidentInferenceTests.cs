using System.Net;
using System.Text;
using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace FoundationalModel.API.Tests;

public sealed class PaymentIncidentInferenceTests
{
    [Fact]
    public void Inference_output_parser_extracts_structured_fields_case_insensitively()
    {
        var result = new InferenceOutputParser().Parse(
            "category: Card Payment\nretryable: YES\naction: Retry the payment");

        Assert.Equal("Card Payment", result.Category);
        Assert.True(result.Retryable);
        Assert.Equal("Retry the payment", result.Action);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Inference_output_parser_returns_invalid_result_for_missing_or_unknown_values()
    {
        var result = new InferenceOutputParser().Parse(
            "Category: Card Payment\nRetryable: Sometimes");

        Assert.Equal("Card Payment", result.Category);
        Assert.Null(result.Retryable);
        Assert.Null(result.Action);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Model_proxy_posts_to_endpoint_and_deserializes_success_response()
    {
        var client = new CapturingProxyClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"InferenceId":"run-1","Category":"Card Payment","Retryable":true,"Action":"Retry","Metadata":{"ModelName":"PAYMENTINCIDENTMODEL","ModelVersion":"1.0","Precision":"FP16","BatchSize":1,"MaxNewTokens":80,"LatencyMs":12}}""",
                Encoding.UTF8,
                "application/json")
        });
        var proxy = new PaymentIncidentModelServiceProxy(
            Options.Create(new PaymentIncidentModelSettings { EndpointId = "endpoint-123" }),
            client);

        var result = await proxy.GenerateAsync(new PaymentIncidentRequest { Incident = "Payment failed" });

        Assert.Equal("run-1", result.InferenceId);
        Assert.Equal("Card Payment", result.Category);
        Assert.True(result.Retryable);
        Assert.Equal("v2/endpoint-123/runsync", client.RequestUri);
        Assert.Contains("Payment failed", client.RequestBody);
    }

    [Fact]
    public async Task Model_proxy_returns_null_for_non_success_response()
    {
        var client = new CapturingProxyClient(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var proxy = new PaymentIncidentModelServiceProxy(
            Options.Create(new PaymentIncidentModelSettings { EndpointId = "endpoint-123" }),
            client);

        var result = await proxy.GenerateAsync(new PaymentIncidentRequest { Incident = "Payment failed" });

        Assert.Null(result);
    }

    [Fact]
    public async Task Inference_service_returns_manual_review_fallback_when_model_returns_null()
    {
        var metrics = new CapturingMetrics();
        var service = new PaymentIncidentInferenceService(
            new InferenceOutputParser(),
            metrics,
            new EmptyInferenceRecordRepository(),
            new StubModelServiceProxy(_ => null));

        var result = await service.Generate(new PaymentIncidentRequest { Incident = "Payment failed" });

        Assert.Equal("Manual Review Required", result.Category);
        Assert.False(result.Retryable);
        Assert.Equal("Escalate the incident for manual review", result.Action);
        Assert.Null(metrics.LastRecord);
    }

    [Fact]
    public async Task Inference_service_records_success_and_returns_model_response()
    {
        var metrics = new CapturingMetrics();
        var expected = Response("Card Payment", true, "Retry");
        var service = new PaymentIncidentInferenceService(
            new InferenceOutputParser(),
            metrics,
            new EmptyInferenceRecordRepository(),
            new StubModelServiceProxy(_ => expected));

        var result = await service.Generate(new PaymentIncidentRequest { Incident = "Payment failed" });

        Assert.Equal(expected.Category, result.Category);
        Assert.Equal(expected.Action, result.Action);
        Assert.Equal(Providers.PAYMENTINCIDENTMODEL.ToString(), metrics.LastRecord?.DeploymentId);
        Assert.True(metrics.LastRecord?.Success);
        Assert.True(metrics.LastRecord?.StructuredOutputValid);
    }

    private static PaymentIncidentResponse Response(string category, bool retryable, string action) => new()
    {
        InferenceId = "run-1",
        Category = category,
        Retryable = retryable,
        Action = action,
        Metadata = new InferenceMetadata
        {
            ModelName = "PAYMENTINCIDENTMODEL",
            ModelVersion = "1.0",
            Precision = "FP16",
            BatchSize = 1,
            MaxNewTokens = 80,
            LatencyMs = 2
        }
    };

    private sealed class CapturingProxyClient : IPaymentIncidentModelProxyClient
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        public string? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        public CapturingProxyClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
            _responseFactory = responseFactory;

        public async Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken cancellationToken = default)
        {
            RequestUri = uri;
            RequestBody = await content.ReadAsStringAsync(cancellationToken);
            return _responseFactory(new HttpRequestMessage(HttpMethod.Post, uri));
        }
    }

    private sealed class StubModelServiceProxy : IPaymentIncidentModelServiceProxy
    {
        private readonly Func<PaymentIncidentRequest, PaymentIncidentResponse?> _responseFactory;

        public StubModelServiceProxy(Func<PaymentIncidentRequest, PaymentIncidentResponse?> responseFactory) =>
            _responseFactory = responseFactory;

        public Task<PaymentIncidentResponse> GenerateAsync(PaymentIncidentRequest request) =>
            Task.FromResult(_responseFactory(request)!);
    }

    private sealed class CapturingMetrics : IInferenceMetrics
    {
        public (string DeploymentId, long LatencyMs, bool Success, bool StructuredOutputValid)? LastRecord { get; private set; }

        public void RecordInference(string deploymentId, long latencyMs, bool success, bool structuredOutputValid) =>
            LastRecord = (deploymentId, latencyMs, success, structuredOutputValid);

        public void RecordFeedback(string deploymentId, bool isCorrect) { }
        public void RecordReviewOutcome(string deploymentId, ReviewStatus status) { }
        public DeploymentMetrics? Get(string deploymentId) => null;
    }

    private sealed class EmptyInferenceRecordRepository : IInferenceRecordRepository
    {
        public Task SaveAsync(InferenceRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<InferenceRecord?> GetAsync(string inferenceId, CancellationToken cancellationToken = default) => Task.FromResult<InferenceRecord?>(null);
    }
}
