namespace FoundationalModel.Models.Dtos.Requests
{
    public class TrainingExperiment
    {
        public string ExperimentName { get; set; } = string.Empty;
        public string TrainingDataset { get; set; } = string.Empty;
        public string TrainingDatasetVersion { get; set; } = string.Empty;
        public string EvaluationDataset { get; set; } = string.Empty;
        public int TrainingExampleCount { get; set; }
        public int EvaluationExampleCount { get; set; }
        public string BaseModel { get; set; } = string.Empty;
        public string LossStrategy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
