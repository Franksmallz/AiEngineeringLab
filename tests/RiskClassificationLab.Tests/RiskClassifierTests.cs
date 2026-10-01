using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;

namespace RiskClassificationLab.Tests;

public sealed class RiskClassifierTests
{
    private readonly RuleBasedRiskClassifier _classifier = new();

    [Theory]
    [InlineData(7499, 12, 10, 2, 30, false, "Low")]
    [InlineData(7501, 12, 10, 2, 30, false, "Medium")]
    [InlineData(5000, 12, 21, 1, 30, false, "Medium")]
    [InlineData(5000, 12, 10, 3, 30, false, "Medium")]
    [InlineData(5000, 12, 10, 1, 6, false, "Low")]
    [InlineData(5000, 12, 10, 1, 30, true, "Medium")]
    [InlineData(5000, 4, 10, 2, 30, false, "Low")]
    [InlineData(7501, 4, 21, 3, 6, true, "High")]
    public void Predict_classifies_each_rule_boundary(
        float amount,
        float hour,
        float transactionCount,
        float failures,
        float beneficiaryAge,
        bool highRiskCountry,
        string expected)
    {
        var result = _classifier.Predict(new TransactionRiskInput
        {
            Amount = amount,
            TransactionHour = hour,
            CustomerTransactionCount24h = transactionCount,
            RecentFailureCount = failures,
            BeneficiaryAgeDays = beneficiaryAge,
            IsHighRiskCountry = highRiskCountry
        });

        Assert.Equal(expected, result);
        Assert.Contains(result, Enum.GetNames<RiskLevel>());
    }
}
