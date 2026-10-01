using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IRiskService : IAutoDependencyService
    {
        TransactionRiskDataResult Generate();
        string Predict(TransactionRiskInput transaction);
        TransactionRiskEvaluationResult Evaluate();
        string Train();
    }
}
