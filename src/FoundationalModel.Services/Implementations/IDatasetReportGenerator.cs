using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public interface IDatasetReportGenerator : IAutoDependencyService
    {
        string Generate(string datasetName, IReadOnlyList<ParsedTrainingExample> examples);
    }
}
