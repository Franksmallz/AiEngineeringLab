namespace RiskInference.Api.Contracts;

// Output contract for POST /risk/predict.
//
// PredictedRisk and FlaggedHighRisk answer two different questions.
// A transaction can have PredictedRisk "Medium" and FlaggedHighRisk true.
public sealed record RiskPredictionResponse(
    // The default multiclass prediction of the model: Low, Medium or High.
    // The threshold does not change it.
    string PredictedRisk,
    // The model score for the High class. It is not a calibrated probability.
    float HighRiskScore,
    // The business decision: HighRiskScore >= Threshold.
    bool FlaggedHighRisk,
    // The configured model version, for example "chapter-06-v1".
    string ModelVersion,
    // The threshold used for this decision.
    double Threshold);
