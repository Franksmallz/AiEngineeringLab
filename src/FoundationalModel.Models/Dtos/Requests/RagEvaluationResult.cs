namespace FoundationalModel.Models.Dtos.Requests
{
    public class RagEvaluationResult
    {
        public int Id { get; set; }
        public string Question { get; set; } = "";
        public List<RetrievedChunk> RetrievedChunks { get; set; } = [];
        public string ActualResponse { get; set; } = "";
        public string ExpectedAnswer { get; set; } = "";
        public int Correctness { get; set; }
        public int Groundedness { get; set; }
        public int RetrievalRelevance { get; set; }
        public long LatencyMs { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public decimal EstimatedCost {  get; set; }
    }

    public class RetrievedChunk
    {
        public string Source { get; set; } = "";
        public string Content { get; set; } = "";
        public double Score { get; set; } 
        public float[] Embeddings { get; set; } = [];
    }
}
