using Anthropic.Models.Messages;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations
{
    public class PaymentIncidentInferenceService : IPaymentIncidentInferenceService
    {
        private readonly IInferenceOutputParser _inferenceOutputParser;
        private readonly IInferenceMetrics _inferenceMetrics;
        private readonly IInferenceRecordRepository _inferenceRecordRepository;
        private readonly IPaymentIncidentModelServiceProxy _paymentIncidentServiceProxy;

        public PaymentIncidentInferenceService(
            IInferenceOutputParser inferenceOutputParser,
            IInferenceMetrics inferenceMetrics,
            IInferenceRecordRepository inferenceRecordRepository,
            IPaymentIncidentModelServiceProxy paymentIncidentServiceProxy)
        {
            _inferenceOutputParser = inferenceOutputParser;
            _inferenceMetrics = inferenceMetrics;
            _inferenceRecordRepository = inferenceRecordRepository;
            _paymentIncidentServiceProxy = paymentIncidentServiceProxy;
        }
        public async Task<PaymentIncidentResponse> Generate(PaymentIncidentRequest request)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var response = await _paymentIncidentServiceProxy.GenerateAsync(request);

                var latencyMs = sw.ElapsedMilliseconds;
                var metadata = new InferenceMetadata
                {
                    BatchSize = 1,
                    LatencyMs = latencyMs,
                    MaxNewTokens = 80,
                    ModelName = Core.Enums.Providers.PAYMENTINCIDENTMODEL.ToString(),
                    ModelVersion = "1.0",
                    Precision = "FP16"
                };

                if (response == null)
                {
                    return PaymentIncidentResponse.Create(Guid.NewGuid().ToString(), metadata);
                }

                _inferenceMetrics.RecordInference(
                    Core.Enums.Providers.PAYMENTINCIDENTMODEL.ToString(),
                    latencyMs,
                    success: true,
                    structuredOutputValid: true);

                var record = new InferenceRecord
                {
                    InferenceId = Guid.NewGuid().ToString(),
                    DeploymentId = Core.Enums.Providers.PAYMENTINCIDENTMODEL.ToString(),
                    RawOutput = JsonSerializer.Serialize(response),
                    Incident = request.Incident,
                    Category = response.Category,
                    LatencyMs = latencyMs,
                    CreatedAt = DateTime.UtcNow,
                    Retryable = response.Retryable,
                    Action = response.Action
                };

                return new PaymentIncidentResponse
                {
                    InferenceId = Guid.NewGuid().ToString(),
                    Category = response.Category,
                    Retryable = response.Retryable,
                    Action = response.Action,
                    Metadata = metadata
                };

            }
            catch (Exception ex)
            {
                var latencyMs = sw.ElapsedMilliseconds;
                var metadata = new InferenceMetadata
                {
                    BatchSize = 1,
                    LatencyMs = latencyMs,
                    MaxNewTokens = 80,
                    ModelName = Core.Enums.Providers.PAYMENTINCIDENTMODEL.ToString(),
                    ModelVersion = "1.0",
                    Precision = "FP16"
                };
                return PaymentIncidentResponse.Create(Guid.NewGuid().ToString(), metadata);
            }
        }
    }
}
