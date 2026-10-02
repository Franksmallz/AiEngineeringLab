using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using System.Globalization;
using System.Text;

namespace RiskClassificationLab.Services.Implementations
{
    public class DatasetWriter : IDatasetWriter
    {
        private readonly IPathResolver _pathResolver;

        public DatasetWriter(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public void CsvDatasetWriter(string filename, List<TransactionRiskData> data)
        {
            var directory = _pathResolver.ResolveConfiguredPath("data/risk-classification");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            //not to contaminate the data
            if (File.Exists(Path.Combine(directory, filename))) return;

            var builder = new StringBuilder();

            builder.AppendLine(
                "Amount,TransactionHour,CustomerTransactionCount24h," +
                "RecentFailureCount,BeneficiaryAgeDays,IsHighRiskCountry,RiskLevel");

            foreach (var transaction in data)
            {
                builder.AppendLine(string.Join(",",
                    transaction.Amount.ToString(CultureInfo.InvariantCulture),
                    transaction.TransactionHour.ToString(CultureInfo.InvariantCulture),
                    transaction.CustomerTransactionCount24h.ToString(CultureInfo.InvariantCulture),
                    transaction.RecentFailureCount.ToString(CultureInfo.InvariantCulture),
                    transaction.BeneficiaryAgeDays.ToString(CultureInfo.InvariantCulture),
                    transaction.IsHighRiskCountry,
                    transaction.RiskLevel));
            }

            File.WriteAllText(Path.Combine(directory, filename), builder.ToString());
        }
    }
}
