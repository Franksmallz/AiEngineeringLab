namespace FoundationalModel.Models.Dtos.Responses
{
    public class ParsedExpectedAnswer
    {
        public string Category { get; set; } = string.Empty;
        public string Retryable { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}
