using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations
{
    public class EvalFileManager : IEvalFileManager
    {
        private readonly IPathResolver _pathResolver;

        public EvalFileManager(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public List<EvaluationResult> Load(string filename)
        {
            var data = new List<EvaluationResult>();

            var path = _pathResolver.ResolveConfiguredPath("finetuning/outputs");
            
            var json = File.ReadAllText(Path.Combine(path, filename));

            var results = JsonSerializer.Deserialize<List<EvaluationResult>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return results ?? [];
        }

        public ManualEvaluation LoadManualEvaluation(string filename, string directory)
        {
            var data = new ManualEvaluation();

            var path = _pathResolver.ResolveConfiguredPath(directory);

            var json = File.ReadAllText(Path.Combine(path, filename));

            var results = JsonSerializer.Deserialize<ManualEvaluation>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return results ?? throw new InvalidOperationException($"Could not load manual evaluation from path {path}");
        }
    }
}
