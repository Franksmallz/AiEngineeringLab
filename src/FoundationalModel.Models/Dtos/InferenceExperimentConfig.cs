namespace FoundationalModel.Models.Dtos
{
    public class InferenceExperimentConfig
    {
        public string ExperimentName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string EvaluationFile { get; set; } = string.Empty;
        public int BatchSize { get; set; }
        public int MaxNewTokens { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public bool DoSample { get; set; }
        public string Precision { get; set; } = string.Empty;
        public int WarmupRuns { get; set; }
        public int Repetitions { get; set; }
        public string OutputFile { get; set; } = string.Empty;
    }
}
