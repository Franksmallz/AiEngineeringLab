using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IDatasetWriter : IAutoDependencyService
    {
        public Task WriteTrainingV2Jsonl(string path, IEnumerable<TraininExampleV2> examples);
        Task WriteDatasetMetadataJson(string filename, DatasetMetadata metadata);
        Task WriteReport(string filename, string directory, string report);
        Task WriteTrainingManifest(string filename, TrainingExperiment experiment);
        Task WriteManualEvaluationTemplate(string directory, string filename, ManualEvaluation manualEvaluation);
    }
}
