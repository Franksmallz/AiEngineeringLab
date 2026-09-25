using System.Text.Json.Serialization;

namespace FoundationalModel.Models.Dtos.Responses
{
    public class InferenceResult
    {
        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;
        [JsonPropertyName("actual")]
        public string Actual { get; set; } = string.Empty;
        [JsonPropertyName("expected")]
        public string Expected {  get; set; }    = string.Empty;
        [JsonPropertyName("generated_tokens")]
        public int GeneratedTokens { get; set; } 
    }
}
