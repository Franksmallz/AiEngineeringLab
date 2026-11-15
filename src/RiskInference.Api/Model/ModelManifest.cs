using System.Text.Json;
using System.Text.RegularExpressions;

namespace RiskInference.Api.Model;

// The manifest travels in the same folder as the model file. It says which
// model the service must load, how to check it, and which decision rule goes
// with it. The service trusts the manifest and checks the model against it.
// A change of model or threshold is a new manifest, so it is a new deployment.
public sealed class ModelManifest
{
    // The versions that this build of the service can serve. A manifest with
    // another version describes features or a decision rule that the code
    // does not implement. The service refuses to start with it.
    public const string SupportedFeatureSchemaVersion = "risk-features-v1";
    public const string SupportedDecisionPolicyVersion = "risk-policy-v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private static readonly Regex Sha256Pattern = new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

    public string ModelVersion { get; init; } = string.Empty;

    // A file name in the folder of the manifest. It is not a path.
    public string ModelFile { get; init; } = string.Empty;

    // The content hash of the model file (see FinalModelStore).
    public string ModelSha256 { get; init; } = string.Empty;

    // Flag High when the High-class score is at or above this value.
    // Use the full-precision Chapter 6 value. A rounded value moves the boundary.
    public double Threshold { get; init; }

    public string FeatureSchemaVersion { get; init; } = string.Empty;
    public string DecisionPolicyVersion { get; init; } = string.Empty;

    public static ModelManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"The model manifest '{manifestPath}' does not exist.");

        return Parse(File.ReadAllText(manifestPath));
    }

    public static ModelManifest Parse(string json)
    {
        ModelManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ModelManifest>(json, JsonOptions)
                ?? throw new InvalidOperationException("The model manifest is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The model manifest is not valid: {ex.Message}", ex);
        }

        var problems = manifest.Validate();
        if (problems.Count > 0)
            throw new InvalidOperationException($"The model manifest is not valid: {string.Join(" ", problems)}");
        return manifest;
    }

    // Returns every problem, so one start-up error shows all of them.
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(ModelVersion))
            problems.Add("modelVersion is required.");

        if (string.IsNullOrWhiteSpace(ModelFile))
            problems.Add("modelFile is required.");
        else if (Path.GetFileName(ModelFile) != ModelFile || ModelFile is "." or "..")
            problems.Add("modelFile must be a file name in the folder of the manifest, not a path.");

        if (!Sha256Pattern.IsMatch(ModelSha256 ?? string.Empty))
            problems.Add("modelSha256 must be 64 hex characters.");

        if (!(double.IsFinite(Threshold) && Threshold > 0 && Threshold < 1))
            problems.Add($"threshold must be greater than 0 and less than 1. Found {Threshold}.");

        if (FeatureSchemaVersion != SupportedFeatureSchemaVersion)
            problems.Add($"featureSchemaVersion '{FeatureSchemaVersion}' is not supported. " +
                         $"This service supports '{SupportedFeatureSchemaVersion}'.");

        if (DecisionPolicyVersion != SupportedDecisionPolicyVersion)
            problems.Add($"decisionPolicyVersion '{DecisionPolicyVersion}' is not supported. " +
                         $"This service supports '{SupportedDecisionPolicyVersion}'.");

        return problems;
    }
}
