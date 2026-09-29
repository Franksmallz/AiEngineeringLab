namespace FoundationalModel.Models.Dtos.Responses
{
    public class PaymentIncidentResponse
    {
        public required string InferenceId { get; init; }
        public required string Category { get; init; }
        public required bool Retryable { get; init; }
        public required string Action { get; init; }
        public required InferenceMetadata Metadata { get; init; }

        public static PaymentIncidentResponse Create(string inferenceId, InferenceMetadata metadata)
        {
            return new PaymentIncidentResponse
            {
                InferenceId = inferenceId,
                Category = "Manual Review Required",
                Retryable = false,
                Action = "Escalate the incident for manual review",
                Metadata = metadata
            };
        }
    }

    public class InferenceMetadata
    {
        public required string ModelName { get; init; }
        public required string ModelVersion { get; init; }
        public required string Precision { get; init; }
        public required int BatchSize { get; init; }
        public required int MaxNewTokens { get; init; } 
        public required long LatencyMs { get; init; }   
    }
}
