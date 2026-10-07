using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public class DatasetProfiler : IDatasetProfiler
    {
        public List<ClassProfile> ProfileDataset(List<TransactionRiskData> data)
        {
            return data
                .GroupBy(d => d.RiskLevel)
                .Select(g => new ClassProfile(
                    RiskLevel: g.Key,
                    Count: g.Count(),
                    Amount: Stats(g.Select(d => d.Amount)),
                    TransactionHour: Stats(g.Select(d => d.TransactionHour)),
                    CustomerTransactionCount24h: Stats(g.Select(d => d.CustomerTransactionCount24h)),
                    RecentFailureCount: Stats(g.Select(d => d.RecentFailureCount)),
                    BeneficiaryAgeDays: Stats(g.Select(d => d.BeneficiaryAgeDays)),
                    HighRiskCountryRate: g.Count(d => d.IsHighRiskCountry) / (double)g.Count()
                ))
                .ToList();
        }

        private FeatureStats Stats(IEnumerable<float> values)
        {
            var data = values.ToArray();
            var mean = data.Average();
            var min = data.Min();
            var max = data.Max();
            return new FeatureStats(mean, min, max);
        }
    }
}
