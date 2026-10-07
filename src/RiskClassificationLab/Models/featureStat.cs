namespace RiskClassificationLab.Models
{
    public record FeatureStats(
     double Mean,
     float Min,
     float Max);

    public record ClassProfile(
    string RiskLevel,
    int Count,
    FeatureStats Amount,
    FeatureStats TransactionHour,
    FeatureStats CustomerTransactionCount24h,
    FeatureStats RecentFailureCount,
    FeatureStats BeneficiaryAgeDays,
    double HighRiskCountryRate);
}
