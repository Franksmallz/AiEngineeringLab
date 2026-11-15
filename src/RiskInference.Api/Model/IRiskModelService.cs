using RiskClassificationLab.Models;
using RiskInference.Api.Contracts;

namespace RiskInference.Api.Model;

public interface IRiskModelService
{
    // Scores one transaction. The input must already be valid.
    // This method is safe to call from many threads at the same time.
    RiskPredictionResponse Predict(TransactionRiskData input);
}
