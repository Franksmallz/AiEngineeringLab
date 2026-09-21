namespace FoundationalModel.Models.Dtos.Requests
{
    public class EvaluationResult
    {
        public string Input { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual {  get; set; } = string.Empty;
    }
}
