using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IDatasetAuditService : IAutoDependencyService
    {
        public void PrintSummary(string name, IReadOnlyList<ParsedTrainingExample> examples);
    }
}
