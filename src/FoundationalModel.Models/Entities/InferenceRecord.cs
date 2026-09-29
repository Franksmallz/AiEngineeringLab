namespace FoundationalModel.Models.Entities
{
    public class InferenceRecord
    {
        public required string InferenceId { get; init; }
        public required string Incident { get; init; }
        public required string RawOutput { get; init; }
        public required string Category { get; init; }
        public required bool Retryable { get; init; }
        public required string Action { get; init; }
        public required long LatencyMs { get; init; }
        public required string DeploymentId { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
