namespace FoundationalModel.Models.Dtos.Responses
{
    public class ToolExecutionResult
    {
        public bool Success { get; set; }
        public string? Data { get; set; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; set; }
    }
}
