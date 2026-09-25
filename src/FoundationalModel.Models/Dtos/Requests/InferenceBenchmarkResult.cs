namespace FoundationalModel.Models.Dtos.Requests
{
    public class InferenceBenchmarkResult
    {
        public string ExperimentName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
        public int EvaluationCases { get; set; }
        public double AverageLatencyMs { get; set; }
        public double TokensPerSecond { get; set; }
        public long MemoryUsedMb { get; set; }
        public int TotalGeneratedTokens { get; set; }
        public double QualityScore { get; set; }
        public int SchemaCompliance { get; set; }
        public int SemanticAccuracy { get; set; }
        public double GPUTimeSeconds { get; set; }
        public bool Successful { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
