using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IEvalFileManager : IAutoDependencyService
    {
        List<EvaluationResult> Load(string filename);
        ManualEvaluation LoadManualEvaluation(string filename, string directory);
    }
}
