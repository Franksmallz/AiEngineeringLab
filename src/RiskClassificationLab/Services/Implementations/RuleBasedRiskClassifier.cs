using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Services.Implementations
{
    public class RuleBasedRiskClassifier : IRiskClassifier
    {
        public string Predict(TransactionRiskInput transaction)
        {
            var score = 0;

            if (transaction.Amount > 7_500)
                score += 2;

            if (transaction.TransactionHour <= 4)
                score += 1;

            if (transaction.CustomerTransactionCount24h > 20)
                score += 2;

            if (transaction.RecentFailureCount >= 3)
                score += 2;

            if (transaction.BeneficiaryAgeDays < 7)
                score += 1;

            if (transaction.IsHighRiskCountry)
                score += 2;

            return score switch
            {
                >= 5 => RiskLevel.High.ToString(),
                >= 2 => RiskLevel.Medium.ToString(),
                _ => RiskLevel.Low.ToString()
            };
        }
    }
}
