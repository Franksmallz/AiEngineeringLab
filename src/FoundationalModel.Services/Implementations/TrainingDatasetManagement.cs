using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Repository.Interfaces;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Repository.Implementations
{
    public class TrainingDatasetManagement : ITrainingDatasetManagement
    {
        private readonly ITrainingExampleCandidateRepository _trainingExampleCandidateRepository;
        private readonly IPathResolver _pathResolver;

        public TrainingDatasetManagement(ITrainingExampleCandidateRepository trainingExampleCandidateRepository, 
            IPathResolver pathResolver)
        {
            _trainingExampleCandidateRepository = trainingExampleCandidateRepository;
            _pathResolver = pathResolver;
        }

        public async Task<TrainingDataVersion> ExportAsync(string version, string outputDirectory)
        {
            var candidates = await _trainingExampleCandidateRepository.GetAllAsync();

            var path = _pathResolver.ResolveConfiguredPath(outputDirectory);
            Directory.CreateDirectory(path);

            var outputPath = Path.Combine(path, $"training-dataset-{version}.jsonl");

            await using var writer = new StreamWriter(outputPath);

            foreach (var candidate in candidates)
            {
                var record = new
                {
                    input = candidate.Input,
                    expected = candidate.ExpectedOutput,
                    sourceInferenceId = candidate.InferenceId,
                    sourceFeedbackId = candidate.FeedbackId
                };

                var json = System.Text.Json.JsonSerializer.Serialize(record);

                await writer.WriteLineAsync(json);
            }

            return new TrainingDataVersion
            {
                DataVersionId = Guid.NewGuid().ToString(),
                Version = version,
                ExampleCount = candidates.Count(),
                Outpath = outputPath,
                CreatedAt = DateTimeOffset.UtcNow,
                Notes = $"Exported from approved corrections"
            };
        }

    }
}
