namespace RiskClassificationLab.Models
{
    // Chapter 6, step 9: both candidates under the same nested threshold
    // protocol, the winner, and the frozen model-and-threshold combination.
    public class ModelSelectionResult
    {
        public string SelectionRule { get; set; } = string.Empty;
        public List<ThresholdEvaluationResult> Candidates { get; set; } = [];
        public string Winner { get; set; } = string.Empty;
        public FrozenModelSpec Frozen { get; set; } = new();
    }

    // Everything needed to repeat the final test exactly. Written before the
    // test set is read.
    public class FrozenModelSpec
    {
        public string Model { get; set; } = string.Empty;
        public string FeatureSet { get; set; } = string.Empty;
        public List<string> Features { get; set; } = [];
        public string FeatureScaling { get; set; } = string.Empty;
        public string TrainingFile { get; set; } = string.Empty;
        public int TrainingRecords { get; set; }
        public int Seed { get; set; }

        // Chosen on the winner's 5,000 out-of-fold training scores only.
        public double DeploymentThreshold { get; set; }
        public string ThresholdSelection { get; set; } = string.Empty;
        public BinaryThresholdMetrics ThresholdOnTrainingOutOfFold { get; set; } = new();
        public double MaximumFlagRate { get; set; }

        // The honest estimate from nested cross-validation, for comparison
        // with the test result.
        public BinaryThresholdMetrics NestedEstimate { get; set; } = new();

        public string ModelFile { get; set; } = string.Empty;
        public string ModelContentSha256 { get; set; } = string.Empty;
        public bool TestSetUsed { get; set; }
    }

    // The single evaluation of the frozen combination on test-1000.csv.
    public class FinalTestResult
    {
        public FrozenModelSpec Frozen { get; set; } = new();
        public string TestFile { get; set; } = string.Empty;
        public int TestRecords { get; set; }
        public List<string> ScoreOrdering { get; set; } = [];
        public BinaryThresholdMetrics Threshold { get; set; } = new();
        public BinaryThresholdMetrics DefaultRule { get; set; } = new();
        public ClassificationMetrics ThreeClassDefaultRule { get; set; } = new();
        public bool FlagRateWithinLimit { get; set; }
    }
}
