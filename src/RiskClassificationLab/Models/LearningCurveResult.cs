namespace RiskClassificationLab.Models
{
    // Learning curves: each candidate model is trained on growing, nested
    // stratified subsets of every fold's training records and scored on the
    // same validation fold.
    public class LearningCurveResult
    {
        public string Dataset { get; set; } = string.Empty;
        public int Seed { get; set; }
        public int FoldCount { get; set; }
        public string SubsetRule { get; set; } = string.Empty;
        public string PredictionRule { get; set; } = string.Empty;
        public List<int> TrainingSizes { get; set; } = [];
        public List<LearningCurvePoint> Points { get; set; } = [];
    }

    // One model at one training size: the aggregate over all 5,000
    // out-of-fold predictions, plus each fold's own metrics.
    public class LearningCurvePoint
    {
        public string Model { get; set; } = string.Empty;
        public string FeatureSet { get; set; } = string.Empty;
        public string FeatureScaling { get; set; } = string.Empty;
        public int TrainingSize { get; set; }
        public bool ScoreOrderingSameInEveryFold { get; set; }
        public ClassificationMetrics Aggregate { get; set; } = new();
        public List<FoldEvaluation> Folds { get; set; } = [];
    }
}
