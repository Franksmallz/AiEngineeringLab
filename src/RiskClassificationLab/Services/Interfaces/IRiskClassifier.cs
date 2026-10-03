using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IRiskClassifier
    {
        string Predict(TransactionRiskInput input);
        TransactionRiskPrediction PredictWithScores(TransactionRiskInput input);
    }
}
