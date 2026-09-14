using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IKnowledgeSearchTool : IAutoDependencyService
    {
        Task<List<KnowledgeSearchResult>> SearchAsync(string query, CancellationToken cancellationToken, int topk = 3);
    }
}
