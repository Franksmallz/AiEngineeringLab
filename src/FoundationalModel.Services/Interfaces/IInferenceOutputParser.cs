using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IInferenceOutputParser : IAutoDependencyService
    {
        ParsedInferenceOutput Parse(string rawOutput);
    }
}
