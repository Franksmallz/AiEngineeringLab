using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Services.Implementations
{
    public class RiskDatasetGenerator : IRiskDatasetGenerator
    {
        private readonly Random _random = new(42);
        public List<TransactionRiskData> Generate(int count)
        {
            var transactions = new List<TransactionRiskData>();

            for(var i = 0; i < count; i++)
            {
                var transaction = GenerateTransaction();

                transaction.RiskLevel = DetermineRiskLevel(transaction);

                transactions.Add(transaction);
            }

            return transactions;
        }

        public TransactionRiskData GenerateRandomTransaction()
        {

            var transaction = GenerateTransaction();

            transaction.RiskLevel = DetermineRiskLevel(transaction);

            return transaction;
        }

        public List<TransactionRiskData> OversampleHighRisk(List<TransactionRiskData> trainingData)
        {
            var highRiskRows = trainingData.
                Where(x => x.RiskLevel == "High").ToList();

            var targetHighCount = trainingData.Count(x => x.RiskLevel == "Medium");

            var additionalNeeded = Math.Max(0, targetHighCount - highRiskRows.Count);

            var oversampledHighRows = Enumerable
                .Range(0, additionalNeeded)
                .Select(_ => highRiskRows[_random.Next(highRiskRows.Count)])
                .ToList();

            var improvedTrainingData = trainingData.Concat(oversampledHighRows).ToList();
            return improvedTrainingData;
        }

        private string DetermineRiskLevel(TransactionRiskData transaction)
        {
            var score = 0.0;

            //individual signals
            score += (double)transaction.Amount / 5000;
            score += transaction.RecentFailureCount * 0.7;
            score += transaction.CustomerTransactionCount24h * 0.08;

            if (transaction.BeneficiaryAgeDays < 7)
                score += 1.0;
            if (transaction.IsHighRiskCountry)
                score += 1.2;
            if (transaction.TransactionHour <= 4)
                score += 0.5;

            //interaction between features
            if (transaction.BeneficiaryAgeDays < 7 && transaction.Amount > 3000)
                score += 1.0;

            var noise = (_random.NextDouble() - 0.5) * 1.0;
            score += 1.0;

            return score switch
            {
                < 3.0 => "Low",
                < 5.5 => "Medium",
                _ => "High"
            };

        }

        private TransactionRiskData GenerateTransaction()
        {
            return new TransactionRiskData
            {
                Amount = (float)(_random.NextDouble() * 10_000),
                TransactionHour = _random.Next(0, 24),
                CustomerTransactionCount24h = _random.Next(1, 30),
                RecentFailureCount = _random.NextDouble() switch
                {
                    < 0.60 => 0,
                    < 0.80 => 1,
                    < 0.90 => 2,
                    < 0.96 => 3,
                    < 0.99 => 4,
                    _ => 5
                },
                BeneficiaryAgeDays = _random.Next(0, 730),
                IsHighRiskCountry = _random.NextDouble() < 0.15
            };
        }
    }
}
