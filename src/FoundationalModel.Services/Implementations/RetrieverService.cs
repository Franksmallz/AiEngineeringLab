using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class RetrieverService : IRetrieverService
    {
        public List<RetrievedChunk> Retrieve(string question, List<DocumentChunk> chunks, int topK = 3)
        {
            var questionWords = question.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();  

            var results = chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = CalculateScore(questionWords, chunk.Content)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(retrievedDocs => new RetrievedChunk
                {
                    Content = retrievedDocs.Chunk.Content,
                    Score = retrievedDocs.Score,
                    Source = retrievedDocs.Chunk.Source,
                    Embeddings = retrievedDocs.Chunk.Embedding,
                })
                .ToList();

            return results;
        }

        private static int CalculateScore(List<string> questionWords, string content)
        {
            var lowerContent = content.ToLowerInvariant();
            return questionWords.Count(word => lowerContent.Contains(word));
        }
    }
}
