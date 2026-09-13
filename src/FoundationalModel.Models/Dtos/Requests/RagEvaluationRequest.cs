using FoundationalModel.Core.Enums;

namespace FoundationalModel.Models.Dtos.Requests;

public sealed class RagEvaluationRequest
{
    public string Question { get; set; } = "";
    public string ExpectedAnswer { get; set; } = "";
    public string ExpectedSource { get; set; } = "";
    public MatchingType MatchingType { get; set; }
}

public sealed class RagCombinedResult
{
    public int Id { get; set; }
    public string Question { get; set; } = "";
    public string ExpectedAnswer { get; set; } = "";
    public string ExpectedSource { get; set; } = "";
    public List<RagRetrievedChunk> KeywordRetrievedChunks { get; set; } = [];
    public List<RagRetrievedChunk> EmbeddingRetrievedChunks { get; set; } = [];
    public string KeywordResponse { get; set; } = "";
    public string EmbeddingResponse { get; set; } = "";
    public long KeywordLatencyMs { get; set; }
    public long EmbeddingLatencyMs { get; set; }
    public decimal KeywordEstimatedCost { get; set; }
    public decimal EmbeddingEstimatedCost { get; set; }
    public long TotalLatencyMs { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public int KeywordRetrievalRelevance { get; set; }
    public int EmbeddingRetrievalRelevance { get; set; }
    public int KeywordCorrectness { get; set; }
    public int EmbeddingCorrectness { get; set; }
    public int KeywordGroundedness { get; set; }
    public int EmbeddingGroundedness { get; set; }
}

public sealed class RagRetrievedChunk
{
    public string Source { get; set; } = "";
    public string Content { get; set; } = "";
    public double? Score { get; set; }
}
