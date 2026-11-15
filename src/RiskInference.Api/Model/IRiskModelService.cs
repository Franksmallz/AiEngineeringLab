using RiskClassificationLab.Models;
using RiskInference.Api.Contracts;

namespace RiskInference.Api.Model;

public interface IRiskModelService
{
    // The model, threshold and policy versions that this service serves.
    ActiveModelInfo Active { get; }

    // Scores one transaction. The input must already be valid.
    // This method is safe to call from many threads at the same time.
    RiskPredictionResponse Predict(TransactionRiskData input);
}
