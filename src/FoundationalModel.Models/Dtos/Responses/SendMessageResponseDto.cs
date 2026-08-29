namespace FoundationalModel.Models.Dtos.Responses
{
    public class SendMessageResponseDto
    {
        public string Model { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long LatencyMs { get; set; }
        public decimal EstimatedCost { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Text { get; set; }
    }
}
