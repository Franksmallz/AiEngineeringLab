using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IDatasetValidator : IAutoDependencyService
    {
        public List<ValidationIssue> Validate(IReadOnlyList<ParsedTrainingExample> examples);
    }
}
