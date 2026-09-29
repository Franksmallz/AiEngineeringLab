namespace FoundationalModel.Models.Dtos.Requests
{
    public class ParsedInferenceOutput
    {
        public string? Category { get; init; }
        public bool? Retryable { get; init; }
        public string? Action { get; init; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Category) && Retryable.HasValue && !string.IsNullOrWhiteSpace(Action);
    }
}
