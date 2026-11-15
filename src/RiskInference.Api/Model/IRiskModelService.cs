using RiskClassificationLab.Models;
using RiskInference.Api.Contracts;

namespace RiskInference.Api.Model;

public interface IRiskModelService
{
    // The model, threshold and policy versions that this service serves.
    ActiveModelInfo Active { get; }

    // Checks again that the manifest and the model file on disk are the ones that were
    // loaded, and that the model still gives the same answer for a fixed test input.
    // Returns null if all is well. Else returns a short fixed code for the problem
    // (for example "model_hash_mismatch"). The code holds no path and no exception text.
    // It never throws.
    string? VerifyIntegrity();

    // Scores one transaction. The input must already be valid.
    // This method is safe to call from many threads at the same time.
    RiskPredictionResponse Predict(TransactionRiskData input);
}
