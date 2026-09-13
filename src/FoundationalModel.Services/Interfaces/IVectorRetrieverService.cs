using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IVectorRetrieverService : IAutoDependencyService
    {
        List<RetrievedChunk> Retrieve(float[] questionEmbedding, List<DocumentChunk> chunks, int topK = 3);
    }
}
