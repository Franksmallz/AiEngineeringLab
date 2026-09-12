namespace FoundationalModel.Models.Configs;

public sealed class RagSettings
{
    public const string SectionName = "Rag";
    public string DataDirectory { get; set; } = "data";
    public string EmbeddingFileName { get; set; } = "embeddings.json";
    public string ResultPath { get; set; } = "experiments/week-06/rag-combined-result.json";
    public int TopK { get; set; } = 3;
    public int MaxTokens { get; set; } = 1024;
}
