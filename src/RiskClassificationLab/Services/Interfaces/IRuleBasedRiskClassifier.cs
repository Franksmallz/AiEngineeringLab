using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IRuleBasedRiskClassifier : IAutoDependencyService
    {
        RiskLevel Predict(TransactionRiskInput transaction);
    }
}
