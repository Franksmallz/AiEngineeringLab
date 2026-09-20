using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IDatasetWriter : IAutoDependencyService
    {
        public Task WriteTrainingV2Jsonl(string path, IEnumerable<TraininExampleV2> examples);
        Task WriteDatasetMetadataJson(string filename, DatasetMetadata metadata);
        Task WriteDatasetReport(string filename, string report);
    }
}
