using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IEmbeddingManagement : IAutoDependencyService
    {
        Task EnsureEmbeddingsAsync(List<DocumentChunk> chunks, string path, CancellationToken cancellationToken);
    }
}
