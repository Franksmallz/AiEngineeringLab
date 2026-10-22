using System.Text.Json.Serialization;

namespace RiskClassificationLab.Models
{
    // The result of one cross-validation run: the aggregate metrics over all
    // out-of-fold predictions, plus each fold's own metrics.
    public class CrossValidationEvaluationResult
    {
        public string Model { get; set; } = string.Empty;
        public string FeatureSet { get; set; } = string.Empty;
        public List<string> Features { get; set; } = [];
        public string FeatureScaling { get; set; } = string.Empty;
        public string Dataset { get; set; } = string.Empty;
        public int Seed { get; set; }
        public int FoldCount { get; set; }
        public string PredictionRule { get; set; } = string.Empty;

        // The class each Score slot belongs to, read from the model's slot names.
        public List<string> ScoreOrdering { get; set; } = [];
        public bool ScoreOrderingSameInEveryFold { get; set; }

        public int OutOfFoldPredictionCount { get; set; }
        public string PredictionsFile { get; set; } = string.Empty;
        public ClassificationMetrics Aggregate { get; set; } = new();
        public List<FoldEvaluation> Folds { get; set; } = [];

        // Every record's prediction. Left out of the API response (5,000 rows);
        // the service writes them to a CSV file instead.
        [JsonIgnore]
        public List<OutOfFoldPrediction> Predictions { get; set; } = [];
    }

    public class FoldEvaluation
    {
        public int Fold { get; set; }
        public int TrainingRecords { get; set; }
        public int ValidationRecords { get; set; }
        public List<string> ScoreOrdering { get; set; } = [];
        public ClassificationMetrics Metrics { get; set; } = new();
    }

    // One record, predicted by the model that did not train on its fold.
    // Scores holds each class's score by name, e.g. Scores["High"].
    public class OutOfFoldPrediction
    {
        public int Fold { get; set; }
        public string Actual { get; set; } = string.Empty;
        public string Predicted { get; set; } = string.Empty;
        public Dictionary<string, float> Scores { get; set; } = [];
    }

    public class ClassificationMetrics
    {
        public int Records { get; set; }
        public double Accuracy { get; set; }
        public int TruePositives { get; set; }
        public int FalsePositives { get; set; }
        public int FalseNegatives { get; set; }
        public double HighPrecision { get; set; }
        public double HighRecall { get; set; }
        public double HighF1 { get; set; }
        public float BusinessCost { get; set; }
        public double HighFlagRate { get; set; }

        // ConfusionMatrix[actual][predicted] = count
        public Dictionary<string, Dictionary<string, int>> ConfusionMatrix { get; set; } = [];
    }
}
