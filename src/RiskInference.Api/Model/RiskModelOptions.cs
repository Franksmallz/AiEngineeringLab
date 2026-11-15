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
// The model identity, its hash and the threshold are not here. They are in
// the manifest, so they cannot differ from the model file next to them.
public sealed class RiskModelOptions
{
    public const string SectionName = "RiskModel";

    // The manifest file. A relative path starts at the solution folder, or at
    // the app folder when there is no solution file (for example in a container).
    [Required]
    public string ManifestPath { get; set; } = string.Empty;

    // Both modes give identical predictions. Only the speed and the memory use differ.
    // Step 4 measured Pooled as much faster, so it is the default.
    [EnumDataType(typeof(InferenceMode))]
    public InferenceMode InferenceMode { get; set; } = InferenceMode.Pooled;

    // How long the host waits for requests in progress when it stops.
    [Range(1, 600)]
    public int ShutdownTimeoutSeconds { get; set; } = 30;
}
