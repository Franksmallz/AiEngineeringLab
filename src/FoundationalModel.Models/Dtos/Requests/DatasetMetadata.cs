namespace FoundationalModel.Models.Dtos.Requests
{
    public class DatasetMetadata
    {
        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public int ExampleCount { get; set; }

        public int CategoryCount { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string Description { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
    }
}

