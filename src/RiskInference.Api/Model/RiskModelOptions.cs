using System.ComponentModel.DataAnnotations;

namespace RiskInference.Api.Model;

// How the service gets a PredictionEngine for each prediction.
public enum InferenceMode
{
    // Create and dispose one engine for every prediction (the Step 2 design).
    EnginePerRequest,
    // Rent an engine from a pool and give it back after the prediction.
    Pooled
}

// Configuration for the "RiskModel" section of appsettings.json.
public sealed class RiskModelOptions
{
    public const string SectionName = "RiskModel";

    // The Chapter 6 model file. A relative path starts at the solution folder,
    // or at the app folder when there is no solution file (for example in a container).
    [Required]
    public string ModelPath { get; set; } = string.Empty;

    // A label that the response returns. It identifies the deployed model.
    [Required]
    public string ModelVersion { get; set; } = string.Empty;

    // Flag High when the High-class score is at or above this value.
    // Use the full-precision Chapter 6 value from the policy file.
    // A rounded value moves the decision boundary.
    [Range(typeof(double), "0", "1", MinimumIsExclusive = true, MaximumIsExclusive = true)]
    public double HighRiskThreshold { get; set; }

    // The content hash that the model file must have. Step 2 checks it
    // when it loads the model, so a changed file cannot load by accident.
    [Required]
    [RegularExpression("^[0-9a-fA-F]{64}$", ErrorMessage = "ModelContentSha256 must be 64 hex characters.")]
    public string ModelContentSha256 { get; set; } = string.Empty;

    // Both modes give identical predictions. Only the speed and the memory use differ.
    [EnumDataType(typeof(InferenceMode))]
    public InferenceMode InferenceMode { get; set; } = InferenceMode.Pooled;
}
