using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class KnowledgeSearchTool : IKnowledgeSearchTool
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorRetrieverService _vectorRetrieverService;
        private readonly IDocumentLoader _documentLoader;

        public KnowledgeSearchTool(IEmbeddingService embeddingService,
            IVectorRetrieverService vectorRetrieverService,
            IDocumentLoader documentLoader)
        {
            _embeddingService = embeddingService;
            _vectorRetrieverService = vectorRetrieverService;
            _documentLoader = documentLoader;
        }

        public async Task<List<KnowledgeSearchResult>> SearchAsync(string query, CancellationToken cancellationToken, int topk = 3)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topk);

            var queryEmbedding = await _embeddingService.CreateEmbeddingAsync(query)
                ?? throw new InvalidOperationException("The embedding provider returned an empty query embedding.");

            var chunks = await _documentLoader.LoadChunksAsync(cancellationToken);
            

            var retrievedResult = _vectorRetrieverService.Retrieve(queryEmbedding, chunks, topk);

            return retrievedResult.
                Select(result => new KnowledgeSearchResult
                {
                    Score = result.Score,
                    Source = result.Source,
                    Content = result.Content,
                }).ToList();
        }
    }
}
