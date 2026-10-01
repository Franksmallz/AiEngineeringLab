using Microsoft.AspNetCore.Http.HttpResults;
using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using RiskClassificationLab.Services.Interfaces.ML;
using System.Diagnostics;

namespace RiskClassificationLab.Services.Implementations
{
    public class RiskService : IRiskService
    {
        private readonly IRiskDatasetGenerator _riskDatasetGenerator;
        private readonly IDatasetSplitter _datasetSplitter;
        private readonly IDatasetWriter _datasetWriter;
        private readonly IDatasetReader _datasetReader;
        private readonly IModelTrainer _modelTrainer;
        private readonly IImplementationResolverService _implementationResolverService;

        public RiskService(IRiskDatasetGenerator riskDatasetGenerator,
            IDatasetSplitter datasetSplitter,
            IDatasetWriter datasetWriter,
            IDatasetReader datasetReader,
            IModelTrainer modelTrainer,
            IImplementationResolverService implementationResolverService)
        {
            _riskDatasetGenerator = riskDatasetGenerator;
            _datasetSplitter = datasetSplitter;
            _datasetWriter = datasetWriter;
            _datasetReader = datasetReader;
            _modelTrainer = modelTrainer;
            _implementationResolverService = implementationResolverService;
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
            var predictionImplementation = _implementationResolverService.ResolveClassifier("Rules");
            var prediction = predictionImplementation.Predict(transaction);

            return prediction.ToString();
        }

        public TransactionRiskEvaluationResult Evaluate()
        {
            var data = _datasetReader.Read("evaluation.csv").ToList();

            var results = data.Select(transaction =>
            {
                var predictionImplementation = _implementationResolverService.ResolveClassifier("Rules");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var rulesPrediction  = predictionImplementation.Predict(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                sw.Stop();

                return (
                
                    Actual: transaction.RiskLevel,
                    Predicted: rulesPrediction.ToString(),
                    Latency: sw.Elapsed.TotalMilliseconds
                );
            }).ToList();

            var evaluationMetrics = ComputeMetrics(results);
            var latencies = results
           .Select(x => x.Latency)
           .OrderBy(x => x)
           .ToList();

            var averageLatency = latencies.Average();

            var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
            var p95Latency = latencies[p95Index];
            evaluationMetrics.Latency = new EvaluationLatencyResult
            {
                AverageLatency = averageLatency,
                P95Latency = p95Latency
            };

            var MLresults = data.Select(transaction =>
            {
                var predictionImplementation = _implementationResolverService.ResolveClassifier("ML");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var rulesPrediction = predictionImplementation.Predict(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                sw.Stop();

                return (

                    Actual: transaction.RiskLevel,
                    Predicted: rulesPrediction.ToString(),
                    Latency: sw.Elapsed.TotalMilliseconds
                );
            }).ToList();

            var MLlatencies = MLresults
            .Select(x => x.Latency)
            .OrderBy(x => x)
            .ToList();

            averageLatency = MLlatencies.Average();

            p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
            p95Latency = MLlatencies[p95Index];

            var MLEvaluationMetrics = ComputeMetrics(MLresults);
            MLEvaluationMetrics.Latency = new EvaluationLatencyResult
            {
                AverageLatency = averageLatency,
                P95Latency = p95Latency
            };

            return new TransactionRiskEvaluationResult
            {
                ML = MLEvaluationMetrics,
                Rules = evaluationMetrics
            };
        }

        private RiskEvaluationResult ComputeMetrics(List<(string Actual, string Predicted, double Latency)> results)
        {
            var classMetrics = Enum
           .GetNames<RiskLevel>()
           .Select(riskClass =>
               CalculateMetrics(riskClass, results))
           .ToList();

            var confusionMatrix = results
            .GroupBy(x => new { x.Actual, x.Predicted })
            .Select(x => new ConfusionMatrix()
            {
                Actual = x.Key.Actual,
                Predicted = x.Key.Predicted,
                Count = x.Count()
            })
            .OrderBy(x => x.Actual)
            .ThenBy(x => x.Predicted)
            .ToList();

            var correct = results.Count(x =>
                x.Actual == x.Predicted);

            var accuracy = (double)correct / results.Count;

            return new RiskEvaluationResult()
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

        private  ClassMetrics CalculateMetrics(
    string riskClass,
    IEnumerable<(string Actual, string Predicted, double Latency)> results)
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

        public string MLPredict(TransactionRiskInput transaction)
        {
            var predictionImplementation = _implementationResolverService.ResolveClassifier("Rules");
            var prediction = predictionImplementation.Predict(transaction);
            return prediction.ToString();
        }

        public EdgeCaseEvaluationResult EvaluateEdgeCases()
        {
            var amountBoundaryBelow = EdgeCaseDataset.AmountBoundaryBelow();
            var amountBoundaryAbove = EdgeCaseDataset.AmountBoundaryAbove();
            var transactionCountBoundaryBelow = EdgeCaseDataset.TransactionCountBoundaryBelow();
            var transactionCountBoundaryAbove = EdgeCaseDataset.TransactionCountBoundaryAbove();
            var recentFailureCountBoundaryBelow = EdgeCaseDataset.RecentFailureCountBoundaryBelow();
            var recentFailureCountBoundaryAbove = EdgeCaseDataset.RecentFailureCountBoundaryAbove();
            var beneficiaryAgeDaysBoundaryBelow = EdgeCaseDataset.BeneficiaryAgeDaysBoundaryBelow();
            var beneficiaryAgeDaysBoundaryAbove = EdgeCaseDataset.BeneficiaryAgeDaysBoundaryAbove();
            var transactionHourBoundaryBelow = EdgeCaseDataset.TransactionHourBoundaryBelow();
            var transactionHourAbove = EdgeCaseDataset.TransactionHourBoundaryAbove();
            var rulesImplementation = _implementationResolverService.ResolveClassifier("Rules");
            var mlImplementation = _implementationResolverService.ResolveClassifier("ML");
            var result = new EdgeCaseEvaluationResult
            {
                AmountBoundary = new EdgeCaseEvaluation
                {
                    Below = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(amountBoundaryBelow),
                        ML = mlImplementation.Predict(amountBoundaryBelow)
                    },
                    Above = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(amountBoundaryAbove),
                        ML = mlImplementation.Predict(amountBoundaryAbove)
                    }
                },
                TransactionCountBoundary = new EdgeCaseEvaluation
                {
                    Below = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(transactionCountBoundaryBelow),
                        ML = mlImplementation.Predict(transactionCountBoundaryBelow)
                    },
                    Above = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(transactionCountBoundaryAbove),
                        ML = mlImplementation.Predict(transactionCountBoundaryAbove)
                    }
                },
                RecentFailureCountBoundary = new EdgeCaseEvaluation
                {
                    Below = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(recentFailureCountBoundaryBelow),
                        ML = mlImplementation.Predict(recentFailureCountBoundaryBelow)
                    },
                    Above = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(recentFailureCountBoundaryAbove),
                        ML = mlImplementation.Predict(recentFailureCountBoundaryAbove)
                    }
                },
                BeneficiaryAgeDaysBoundary = new EdgeCaseEvaluation
                {
                    Below = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(beneficiaryAgeDaysBoundaryBelow),
                        ML = mlImplementation.Predict(beneficiaryAgeDaysBoundaryBelow)
                    },
                    Above = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(beneficiaryAgeDaysBoundaryAbove),
                        ML = mlImplementation.Predict(beneficiaryAgeDaysBoundaryAbove)
                    }
                },
                TransactionHourBoundary = new EdgeCaseEvaluation
                {
                    Below = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(transactionHourBoundaryBelow),
                        ML = mlImplementation.Predict(transactionHourBoundaryBelow)
                    },
                    Above = new EdgeCasePrediction
                    {
                        Rules = rulesImplementation.Predict(transactionHourAbove),
                        ML = mlImplementation.Predict(transactionHourAbove)
                    }
                }
            };
            return result;
        }
    }
}
