using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IDocumentLoader : IAutoDependencyService
    {
        Task<List<DocumentChunk>> LoadChunksAsync(CancellationToken cancellationToken);
    }
}
