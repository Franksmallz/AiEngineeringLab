namespace FoundationalModel.Models.Configs
{
    public class OpenAiProviderSettings
    {
        public const string SectionName = "EmbeddingProviderSeetings:OpenAi";
        public string ApiKey { get; set; }
        public string Model { get; set; }
    }
}
