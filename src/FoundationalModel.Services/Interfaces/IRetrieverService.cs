using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IRetrieverService : IAutoDependencyService
    {
        List<RetrievedChunk> Retrieve(string question, List<DocumentChunk> chunks, int topK = 3);
    }
}
