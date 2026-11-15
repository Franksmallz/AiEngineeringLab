namespace RiskInference.Api.Model;

// What the service is serving now. It comes from the checked manifest.
public sealed record ActiveModelInfo(
    string ModelVersion,
    string DecisionPolicyVersion,
    string FeatureSchemaVersion,
    string ModelSha256,
    double Threshold);
