using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using System.Text.Json;

namespace FoundationalModel.Services.Interfaces
{
    public interface IExecuteAgenticToolService : IAutoDependencyService
    {
        Task<ToolExecutionResult> ExecuteToolAsync(string toolName, IReadOnlyDictionary<string, JsonElement> inputs, UserContext user);
    }
}
