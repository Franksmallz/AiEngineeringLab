using Microsoft.ML;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using System.Transactions;

namespace RiskClassificationLab.Services.Implementations.ML
{
    public class MlRiskClassifier : IRiskClassifier
    {
        private readonly PredictionEngine<TransactionRiskData, TransactionRiskPrediction>
        _predictionEngine;

        public MlRiskClassifier(IPathResolver resolver)
        {

            var mlContext = new MLContext();
            var directory = resolver.ResolveConfiguredPath("models");
            var model = mlContext.Model.Load(
                Path.Combine(directory, "risk-classifier.zip"),
                out _);

            _predictionEngine =
                mlContext.Model.CreatePredictionEngine<
                    TransactionRiskData,
                    TransactionRiskPrediction>(model);
        }
        public string Predict(TransactionRiskInput transaction)
        {
            var data = new TransactionRiskData
            {
                Amount = transaction.Amount,
                TransactionHour = transaction.TransactionHour,
                CustomerTransactionCount24h =
                transaction.CustomerTransactionCount24h,
                RecentFailureCount = transaction.RecentFailureCount,
                BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                IsHighRiskCountry = transaction.IsHighRiskCountry
            };

            var prediction = _predictionEngine.Predict(data);

            return prediction.RiskLevel;
        }

        public TransactionRiskPrediction PredictWithScores(TransactionRiskInput input)
        {
            var data = new TransactionRiskData
            {
                Amount = input.Amount,
                TransactionHour = input.TransactionHour,
                CustomerTransactionCount24h =
                input.CustomerTransactionCount24h,
                RecentFailureCount = input.RecentFailureCount,
                BeneficiaryAgeDays = input.BeneficiaryAgeDays,
                IsHighRiskCountry = input.IsHighRiskCountry
            };

            return _predictionEngine.Predict(data);
        }

    }
}
        