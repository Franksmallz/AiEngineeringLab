namespace FoundationalModel.Models.Dtos.Requests
{
    public class ParsedTrainingExample
    {
        public string Input { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Retryable {  get; set; } = string.Empty;
        public string Action {  get; set; } = string.Empty;
    }
}
