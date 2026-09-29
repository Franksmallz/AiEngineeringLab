namespace FoundationalModel.Models.Entities
{
    public class ModelDeployment
    {
        public required string DeploymentId { get; init; }
        public required string ModelId { get; init; }
        public required string ModelVersion { get; init; }
        public required string AdapterVersion   { get; init; }
        public required string Precision { get; init; }
        public required int BatchSize { get; init; }
        public required int MaxNewTokens { get; init; }
        public required bool DoSample { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public string? Notes { get; init; }

    }
}
