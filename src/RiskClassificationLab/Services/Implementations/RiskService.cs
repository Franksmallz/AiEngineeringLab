using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using RiskClassificationLab.Services.Interfaces.ML;

namespace RiskClassificationLab.Services.Implementations
{
    public class RiskService : IRiskService
    {
        private readonly IRiskDatasetGenerator _riskDatasetGenerator;
        private readonly IDatasetSplitter _datasetSplitter;
        private readonly IDatasetWriter _datasetWriter;
        private readonly IRuleBasedRiskClassifier _ruleBasedClassifier;
        private readonly IDatasetReader _datasetReader;
        private readonly IModelTrainer _modelTrainer;

        public RiskService(IRiskDatasetGenerator riskDatasetGenerator,
            IDatasetSplitter datasetSplitter,
            IDatasetWriter datasetWriter,
            IRuleBasedRiskClassifier ruleBasedClassifier,
            IDatasetReader datasetReader,
            IModelTrainer modelTrainer)
        {
            _riskDatasetGenerator = riskDatasetGenerator;
            _datasetSplitter = datasetSplitter;
            _datasetWriter = datasetWriter;
            _ruleBasedClassifier = ruleBasedClassifier;
            _datasetReader = datasetReader;
            _modelTrainer = modelTrainer;
        }

        public TransactionRiskDataResult Generate()
        {
            var transactions = _riskDatasetGenerator.Generate(400);

            var (train, evaluation) = _datasetSplitter.Split(transactions);

             _datasetWriter.CsvDatasetWriter(
            "train.csv",
            train);

            _datasetWriter.CsvDatasetWriter(
                "evaluation.csv",
                evaluation);

            var distribution = new TransactionRiskDataResult()
            {
                Total = new Data
                {
                    Count = transactions.Count,
                    Distribution = transactions.GroupBy(x => x.RiskLevel)
                    .ToDictionary(x => x.Key, x => x.Count())
                },

                Train = new Data
                {
                    Count = train.Count,
                    Distribution = train.GroupBy(x => x.RiskLevel)
                    .ToDictionary(x => x.Key, x => x.Count())
                },

                Evaluation = new Data
                {
                    Count = evaluation.Count,
                    Distribution = evaluation.GroupBy(x => x.RiskLevel)
                    .ToDictionary(x => x.Key, x => x.Count())
                },
            };



            return distribution;
        }

        public string Predict(TransactionRiskInput transaction)
        {
            var prediction = _ruleBasedClassifier.Predict(transaction);

            return prediction.ToString();
        }

        public TransactionRiskEvaluationResult Evaluate()
        {
            var data = _datasetReader.Read("evaluation.csv").ToList();

            var results = data.Select(transaction =>
            {
                var prediction = Predict(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                return(
                
                    Actual: transaction.RiskLevel,
                    Predicted: prediction.ToString()
                );
            }).ToList();

            var classMetrics = Enum
            .GetNames<RiskLevel>()
            .Select(riskClass =>
                CalculateMetrics(riskClass, results))
            .ToList();

            var confusionMatrix = results
            .GroupBy(x => new { x.Actual, x.Predicted })
            .Select(x => new ConfusionMatrix()
            {
               Actual =  x.Key.Actual,
               Predicted =  x.Key.Predicted,
               Count = x.Count()
            })
            .OrderBy(x => x.Actual)
            .ThenBy(x => x.Predicted)
            .ToList();

            var correct = results.Count(x =>
                x.Actual == x.Predicted);

            var accuracy = (double)correct / results.Count;

            return new()
            {
                Total = results.Count,
                Correct = correct,
                InCorrect = results.Count - correct,
                Accuracy = accuracy,
                ConfusionMatrix = confusionMatrix,
                ClassMetrics = classMetrics
            };
        }

        public string Train()
        {
            _modelTrainer.Train("train.csv", "risk-classifier.zip");

            return "Model training completed successfully";
        }

        private static ClassMetrics CalculateMetrics(
    string riskClass,
    IEnumerable<(string Actual, string Predicted)> results)
        {
            var data = results.ToList();

            var tp = data.Count(x =>
                x.Actual == riskClass &&
                x.Predicted == riskClass);

            var fp = data.Count(x =>
                x.Actual != riskClass &&
                x.Predicted == riskClass);

            var fn = data.Count(x =>
                x.Actual == riskClass &&
                x.Predicted != riskClass);

            var precision = tp + fp == 0
                ? 0
                : (double)tp / (tp + fp);

            var recall = tp + fn == 0
                ? 0
                : (double)tp / (tp + fn);

            var f1 = precision + recall == 0
                ? 0
                : 2 * precision * recall / (precision + recall);

            return new ClassMetrics
            {
                Class = riskClass,
                TruePositive = tp,
                FalsePositive = fp,
                FalseNegative = fn,
                Precision = precision,
                Recall = recall,
                F1 = f1
            };
        }
    }
}
