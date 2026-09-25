namespace FoundationalModel.Models.Dtos.Responses
{
    public sealed class SemanticEvaluation
    {
        public string Input { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public bool CategoryCorrect { get; set; }
        public bool RetryableCorrect { get; set; }
        public bool ActionCorrect { get; set; }
        public bool HasContradictionOrHallucination { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
