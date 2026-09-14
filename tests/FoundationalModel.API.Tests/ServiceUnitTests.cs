using System.Text.Json;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.API.Tests;

public sealed class ServiceUnitTests
{
    [Fact]
    public void Document_chunk_service_preserves_paragraphs_and_respects_maximum_length()
    {
        var service = new DocumentChunkService();

        var chunks = service.ChunkDocument("First paragraph.\n\nSecond paragraph.", 100);

        Assert.Equal(["First paragraph.\n\nSecond paragraph."], chunks);
        Assert.All(service.ChunkDocument(new string('x', 11), 5), chunk => Assert.InRange(chunk.Length, 1, 5));
    }

    [Fact]
    public void Vector_retriever_orders_by_cosine_similarity_and_applies_top_k()
    {
        var chunks = new List<DocumentChunk>
        {
            new() { Source = "best.md", Content = "best", Embedding = [1, 0] },
            new() { Source = "other.md", Content = "other", Embedding = [0, 1] }
        };

        var result = new VectorRetrieverService().Retrieve([1, 0], chunks, 1);

        var match = Assert.Single(result);
        Assert.Equal("best.md", match.Source);
        Assert.Equal(1, match.Score);
        Assert.Equal([1f, 0f], match.Embeddings);
    }

    [Fact]
    public async Task Agentic_tool_service_returns_payment_status_for_valid_payment_id()
    {
        var service = new ExecuteAgenticToolService(new StubPaymentTools(), new StubKnowledgeSearchTool());

        var result = await service.ExecuteToolAsync(
            "get_payment_status",
            new Dictionary<string, JsonElement> { ["paymentId"] = JsonSerializer.SerializeToElement("pay_123") },
            AuthorizedUser());

        var execution = Assert.IsType<ToolExecutionResult>(result);
        Assert.True(execution.Success);
        Assert.Contains("pay_123", execution.Data);
    }

    [Fact]
    public async Task Agentic_tool_service_rejects_missing_required_argument()
    {
        var service = new ExecuteAgenticToolService(new StubPaymentTools(), new StubKnowledgeSearchTool());

        var result = await service.ExecuteToolAsync("get_payment_status", new Dictionary<string, JsonElement>(), AuthorizedUser());

        var execution = Assert.IsType<ToolExecutionResult>(result);
        Assert.False(execution.Success);
        Assert.Equal("MISSING_ARGUMENT", execution.ErrorCode);
    }

    [Fact]
    public async Task Knowledge_search_passes_requested_top_k_to_vector_retriever()
    {
        var vector = new CapturingVectorRetriever();
        var service = new KnowledgeSearchTool(new StubEmbeddingService(), vector, new StubDocumentLoader());

        var result = await service.SearchAsync("retries", CancellationToken.None, 2);

        Assert.Equal(2, vector.ReceivedTopK);
        Assert.Single(result);
        Assert.Equal("docs.md", result[0].Source);
    }

    private sealed class StubPaymentTools : IPaymentTools
    {
        public Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId) => Task.FromResult(new PaymentStatusResult { PaymentId = paymentId, Status = "Pending", TenantId = "test_tenant" });
        public Task<decimal> GetCustomerBalance(string customerId) => Task.FromResult(100m);
    }

    private static UserContext AuthorizedUser() => new()
    {
        TenantId = "test_tenant",
        UserId = "test_user",
        Permissions = ["payment.read", "balance.read", "knowledge_read"]
    };

    private sealed class StubKnowledgeSearchTool : IKnowledgeSearchTool
    {
        public Task<List<KnowledgeSearchResult>> SearchAsync(string query, CancellationToken cancellationToken, int topk = 3) =>
            Task.FromResult(new List<KnowledgeSearchResult> { new() { Source = "docs.md", Content = "result", Score = 1 } });
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<float[]> CreateEmbeddingAsync(string text) => Task.FromResult(new[] { 1f, 0f });
    }

    private sealed class StubDocumentLoader : IDocumentLoader
    {
        public Task<List<DocumentChunk>> LoadChunksAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new List<DocumentChunk> { new() { Source = "docs.md", Content = "result", Embedding = [1, 0] } });
    }

    private sealed class CapturingVectorRetriever : IVectorRetrieverService
    {
        public int ReceivedTopK { get; private set; }
        public List<RetrievedChunk> Retrieve(float[] questionEmbedding, List<DocumentChunk> chunks, int topK = 3)
        {
            ReceivedTopK = topK;
            return [new RetrievedChunk { Source = "docs.md", Content = "result", Score = 1 }];
        }
    }
}
