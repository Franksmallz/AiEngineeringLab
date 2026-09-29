namespace FoundationalModel.Models.Dtos.Requests
{
    public class TrainingDataVersion
    {
        public required string DataVersionId { get; init; }
        public required string Version { get; init; }
        public required int ExampleCount { get; init; }
        public required string Outpath { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public string? Notes { get; init; }
    }
}
