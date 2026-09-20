using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations
{
    public class DatasetWriter : IDatasetWriter
    {
        private readonly IPathResolver _pathResolver;
        private readonly DatasetEngineeringSettings _datasetSettings;

        public DatasetWriter(IPathResolver pathResolver,
            IOptions<DatasetEngineeringSettings> options)
        {
            _pathResolver = pathResolver;
            _datasetSettings = options.Value;
        }

        public async Task WriteTrainingV2Jsonl(string filename, IEnumerable<TraininExampleV2> examples)
        {
            var path = _pathResolver.ResolveConfiguredPath(_datasetSettings.DataDirectory);

            var fullpath = Path.Combine(path, filename);

            if (File.Exists(fullpath))
            {
                var fileInfo = new FileInfo(fullpath);

                if (fileInfo.Length > 0)
                {
                    File.WriteAllText(fullpath, string.Empty);
                }
            }

            using var writer = new StreamWriter(fullpath);

            foreach(var example in examples)
            {
                var json = JsonSerializer.Serialize(example);
                await writer.WriteAsync(json);
                writer.Write('\n');
            }
        }

        public async Task WriteDatasetMetadataJson(string filename, DatasetMetadata metadata)
        {
            var path = _pathResolver.ResolveConfiguredPath(_datasetSettings.DataDirectory);

            var fullpath = Path.Combine(path, filename);

            if (File.Exists(fullpath))
            {
                var fileInfo = new FileInfo(fullpath);

                if (fileInfo.Length > 0)
                {
                    File.WriteAllText(fullpath, string.Empty);
                }
            }

            using var writer = new StreamWriter(fullpath);

            var json = JsonSerializer.Serialize(metadata);
            await writer.WriteAsync(json);
            writer.Write('\n');
        }
    }
}
