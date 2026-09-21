namespace FoundationalModel.Models.Dtos.Responses
{
    public class ManualSemanticScore
    {
        public int CaseNumber { get; set; }
        public bool CategoryCorrect { get; set; }
        public bool RetryableCorrect { get; set; }
        public bool ActionCorrect { get; set; }
        public bool HasContradictionOrHallucination { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
