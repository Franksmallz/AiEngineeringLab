namespace FoundationalModel.Models.Configs
{
    public class AnthropicProviderSettings
    {
        public const string SectionName = "ModelProviderSeetings:Anthropic";
        public string ApiKey { get; set; }
        public int Timeout { get; set; }
        public int MaxRetries { get; set; }
        public int MaxTokens { get; set; }
        public Dictionary<string, ModelCost> Models { get; set; } = new();
    }

    public class ModelCost
    {
        public decimal InputPerMillion { get; set; }
        public decimal OutputPerMillion { get; set; }
    }
}
