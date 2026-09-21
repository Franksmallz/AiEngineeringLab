namespace FoundationalModel.Models.Dtos.Responses
{
    public class EvaluationScore
    {
        public bool CategoryCorrect { get; set; }
        public bool RetryableCorrect { get; set; }
        public bool ActionCorrect { get; set; }
        public bool SchemaCompliant { get; set; }
        public bool HasContradictionOrHallucination { get; set; }
    }
}
