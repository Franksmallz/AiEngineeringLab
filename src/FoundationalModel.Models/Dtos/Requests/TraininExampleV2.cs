using System.Text.Json.Serialization;

namespace FoundationalModel.Models.Dtos.Requests
{
    public class TraininExampleV2
    {
        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;
        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;

        public static TraininExampleV2 Create(string input, string category, string retryable, string action)
        {
            return new TraininExampleV2
            {
                Input = input,
                Output =
                $"Category: {category}\n" +
                $"Retryable: {retryable}\n" +
                $"Action: {action}"
            };
        }
    }

}
