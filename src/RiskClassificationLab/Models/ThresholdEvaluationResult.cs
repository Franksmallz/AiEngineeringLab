namespace RiskClassificationLab.Models
{
    // Chapter 6, step 8: a High / Not High decision made with a threshold on
    // the High score, chosen by nested cross-validation.
    public class ThresholdEvaluationResult
    {
        public string Model { get; set; } = string.Empty;
        public string FeatureSet { get; set; } = string.Empty;
        public string FeatureScaling { get; set; } = string.Empty;
        public string Dataset { get; set; } = string.Empty;
        public int Seed { get; set; }
        public string DecisionRule { get; set; } = string.Empty;
        public string SelectionRule { get; set; } = string.Empty;
        public double MaximumFlagRate { get; set; }

        // 50 / (1,000 + 50): the cost-optimal threshold if the scores were
        // honest probabilities. Reported for comparison only.
        public double CostRatioThreshold { get; set; }

        // Honest estimate: each fold graded with a threshold chosen without it.
        public BinaryThresholdMetrics NestedAggregate { get; set; } = new();
        public List<NestedThresholdFold> Folds { get; set; } = [];

        // Comparisons on the same 5,000 outer predictions.
        public BinaryThresholdMetrics DefaultRule { get; set; } = new();
        public BinaryThresholdMetrics CostRatioRule { get; set; } = new();

        // Exploration only: the threshold chosen and graded on the same 5,000
        // predictions. Optimistic; not a performance estimate.
        public BinaryThresholdMetrics InSampleBest { get; set; } = new();
        public List<BinaryThresholdMetrics> InSampleGrid { get; set; } = [];
        public string SweepFile { get; set; } = string.Empty;
    }

    public class NestedThresholdFold
    {
        public int Fold { get; set; }
        public int InnerRecords { get; set; }
        public List<int> InnerTrainingRecords { get; set; } = [];
        public int OuterTrainingRecords { get; set; }
        public double SelectedThreshold { get; set; }

        // The chosen threshold on the inner scores it was chosen from (optimistic).
        public BinaryThresholdMetrics Inner { get; set; } = new();

        // The chosen threshold on the locked fold (honest).
        public BinaryThresholdMetrics Outer { get; set; } = new();
        public BinaryThresholdMetrics OuterDefaultRule { get; set; } = new();
    }

    // High versus Not High. Positive = flagged High.
    public class BinaryThresholdMetrics
    {
        public double? Threshold { get; set; }
        public int Records { get; set; }
        public int TruePositives { get; set; }
        public int FalsePositives { get; set; }
        public int FalseNegatives { get; set; }
        public int TrueNegatives { get; set; }
        public double HighPrecision { get; set; }
        public double HighRecall { get; set; }
        public double HighF1 { get; set; }
        public double BusinessCost { get; set; }
        public double HighFlagRate { get; set; }
    }
}
