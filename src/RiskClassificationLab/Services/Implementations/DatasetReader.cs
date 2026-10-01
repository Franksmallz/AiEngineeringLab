using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using System.Globalization;

namespace RiskClassificationLab.Services.Implementations
{
    public class DatasetReader : IDatasetReader
    {
        private readonly IPathResolver _pathResolver;

        public DatasetReader(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public IEnumerable<TransactionRiskData> Read(string filename)
        {
            var directory = _pathResolver.ResolveConfiguredPath("data/risk-classification");
            var path = Path.Combine(directory, filename);

            return File.ReadLines(path)
                .Skip(1)
                .Select(Parse);
        }

        private static TransactionRiskData Parse(string line)
        {
            var values = line.Split(',');

            return new TransactionRiskData
            {
                Amount = float.Parse(values[0], CultureInfo.InvariantCulture),
                TransactionHour = float.Parse(values[1], CultureInfo.InvariantCulture),
                CustomerTransactionCount24h =
                    float.Parse(values[2], CultureInfo.InvariantCulture),
                RecentFailureCount =
                    float.Parse(values[3], CultureInfo.InvariantCulture),
                BeneficiaryAgeDays =
                    float.Parse(values[4], CultureInfo.InvariantCulture),
                IsHighRiskCountry = bool.Parse(values[5]),
                RiskLevel = values[6]
            };
        }
    }
}
