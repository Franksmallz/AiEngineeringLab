using System.Text.Json;

namespace FoundationalModel.Services.Interfaces
{
    public interface IExecuteAgenticToolService : IAutoDependencyService
    {
        Task<object> ExecuteToolAsync(string toolName, IReadOnlyDictionary<string, JsonElement> inputs);
    }
}
