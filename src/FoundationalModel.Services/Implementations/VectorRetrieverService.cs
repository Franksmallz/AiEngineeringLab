using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class VectorRetrieverService : IVectorRetrieverService
    {
        public List<RetrievedChunk> Retrieve(float[] questionEmbedding, List<DocumentChunk> chunks, int topK = 3)
        {
            return chunks.
                    Select(chunk => new
                    {
                        Chunk = chunk,
                        Score = CosineSimilarity(questionEmbedding, chunk.Embedding)
                    })
                    .OrderByDescending(x => x.Score)
                    .Take(topK)
                    .Select(x => new RetrievedChunk
                    {
                        Content = x.Chunk.Content,
                        Score = x.Score,
                        Source = x.Chunk.Source,
                        Embeddings = x.Chunk.Embedding
                    })
                    .ToList();
        }

        private static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0) return 0;

            return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
    }
}
