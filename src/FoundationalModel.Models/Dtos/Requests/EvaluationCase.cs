namespace FoundationalModel.Models.Dtos.Requests
{
    public class EvaluationCase
    {
        public int Id { get; set; } 
        public string Input { get; set; }
        public List<string> ExpectedBehavior { get; set; } = [];
        public string ActualResponse { get; set; } = "";
        public int Correctness { get; set; }
        public int Relevance { get; set; }
        public int InstructionFollowing { get; set; }
        public long LatencyMs { get; set; }
        public decimal EstimatedCost { get; set; }
    }
}
