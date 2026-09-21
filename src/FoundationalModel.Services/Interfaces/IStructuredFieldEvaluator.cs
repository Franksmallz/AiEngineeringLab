using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IStructuredFieldEvaluator : IAutoDependencyService
    {
        StructuredFieldScore Score(string expected, string actual);
    }
}
