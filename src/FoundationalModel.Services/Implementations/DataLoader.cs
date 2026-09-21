using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations
{
    public class DataLoader : IDataLoader
    {
        private readonly IPathResolver _pathResolver;
        private readonly DatasetEngineeringSettings _datasetSettings;

        public DataLoader(IPathResolver pathResolver, 
            IOptions<DatasetEngineeringSettings> options)
        {
            _pathResolver = pathResolver;
            _datasetSettings = options.Value;
        }

        public List<TrainingExample> Load(string filename)
        {
            var examples = new List<TrainingExample>();

            var path = _pathResolver.ResolveConfiguredPath(_datasetSettings.DataDirectory);
           foreach(var line in File.ReadLines(Path.Combine(path, filename)))
            {
                if(string.IsNullOrWhiteSpace(line)) continue;

                var example = JsonSerializer.Deserialize<TrainingExample>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if(example is not  null) examples.Add(example);
            }

           return examples;
        }
    }
}
