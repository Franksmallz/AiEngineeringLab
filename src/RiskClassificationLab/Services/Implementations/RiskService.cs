using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.LightGbm;
using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations.ML;
using RiskClassificationLab.Services.Interfaces;
using RiskClassificationLab.Services.Interfaces.ML;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        private readonly IDatasetProfiler _datasetProfiler;
        private readonly ICrossValidationEvaluator _crossValidationEvaluator;
        private readonly IFinalModelStore _finalModelStore;

        public RiskService(IRiskDatasetGenerator riskDatasetGenerator,
            IDatasetSplitter datasetSplitter,
            IDatasetWriter datasetWriter,
            IDatasetReader datasetReader,
            IModelTrainer modelTrainer,
            IImplementationResolverService implementationResolverService,
            IDatasetProfiler datasetProfiler,
            ICrossValidationEvaluator crossValidationEvaluator,
            IFinalModelStore finalModelStore)
        {
            _finalModelStore = finalModelStore;
            _crossValidationEvaluator = crossValidationEvaluator;
            _riskDatasetGenerator = riskDatasetGenerator;
            _datasetSplitter = datasetSplitter;
            _datasetWriter = datasetWriter;
            _datasetReader = datasetReader;
            _modelTrainer = modelTrainer;
            _implementationResolverService = implementationResolverService;
            _datasetProfiler = datasetProfiler;
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

        private ClassMetrics CalculateMetrics(
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

        public TransactionRiskPrediction PredictWithScores(TransactionRiskInput input)
        {
            var data = new TransactionRiskData
            {
                Amount = input.Amount,
                TransactionHour = input.TransactionHour,
                CustomerTransactionCount24h = input.CustomerTransactionCount24h,
                RecentFailureCount = input.RecentFailureCount,
                BeneficiaryAgeDays = input.BeneficiaryAgeDays,
                IsHighRiskCountry = input.IsHighRiskCountry
            };

            var mlImplementation = _implementationResolverService.ResolveClassifier("ML");
            return mlImplementation.PredictWithScores(input);
        }

        public List<MetricsThreshold> EvaluateWithScores()
        {
            var data = _datasetReader.Read("evaluation.csv").ToList();

            var thresholds = HighRiskScores();
            var metricsPerThreshold = new List<MetricsThreshold>();
            foreach (var threshold in thresholds)
            {
                var metrics = CalculateMetricsForThreshold(data, threshold);
                var totalFlagged = metrics.TP + metrics.FP;

                var flagRate =
                    (double)totalFlagged / data.Count;

                var meetsConstraint = flagRate <= 0.40;
                if(meetsConstraint)
                {
                    metricsPerThreshold.Add(new MetricsThreshold
                    {
                        Threshold = threshold,
                        TP = metrics.TP,
                        FP = metrics.FP,
                        FN = metrics.FN,
                        TN = metrics.TN,
                        Precision = metrics.Precision,
                        Recall = metrics.Recall,
                        F1 = metrics.F1,
                        BusinessCost = metrics.BusinessCost,
                        flagRate = flagRate,
                        meetsConstraints = meetsConstraint
                    });
                }
            }

            return metricsPerThreshold;

        }

        public (int TP, int FP, int FN, int TN, double Precision, double Recall, double F1, float BusinessCost) CalculateMetricsForThreshold(List<TransactionRiskData> data, float threshold)
        {
            var truePositive = 0;
            var falsePositive = 0;
            var falseNegative = 0;
            var trueNegative = 0;
            const float falseNegativeAmount = 1_000;
            const float falsePositiveAmount = 50;
            var mlPrediction = _implementationResolverService.ResolveClassifier("ML");
            foreach (var transaction in data)
            {
                var rulesPrediction = mlPrediction.PredictWithScores(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                var highRiskScore = rulesPrediction.Score[1];
                var predictedHighRisk = highRiskScore >= threshold;
                var actuallyHighRisk = transaction.RiskLevel == "High";

                if (actuallyHighRisk && predictedHighRisk) truePositive++;
                else if (!actuallyHighRisk && predictedHighRisk) falsePositive++;
                else if (actuallyHighRisk && !predictedHighRisk) falseNegative++;
                else trueNegative++;
            }

            var precision = truePositive + falsePositive == 0
                ? 0
                : (double)truePositive / (truePositive + falsePositive);

            var recall = truePositive + falseNegative == 0
                ? 0
                : (double)truePositive / (truePositive + falseNegative);

            var f1 = precision + recall == 0
                ? 0
                : 2 * precision * recall / (precision + recall);

            var businessCost = falseNegative * falseNegativeAmount + falsePositive * falsePositiveAmount;
            return (truePositive, falsePositive, falseNegative, trueNegative, precision, recall, f1, businessCost);
        }

        public float[] HighRiskScores()
        {
            var data = _datasetReader.Read("evaluation.csv").ToList();

            var mlPrediction = _implementationResolverService.ResolveClassifier("ML");
            return  data.Select(transaction =>
            {
                var rulesPrediction = mlPrediction.PredictWithScores(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                return rulesPrediction.Score[1];

            })
            .Distinct()
            .OrderBy(x => x)
            .ToArray();



        }

        public List<ClassProfile> ProfileDataset()
        {
            var data = _datasetReader.Read("train.csv").ToList();
            var profile = _datasetProfiler.ProfileDataset(data);
            return profile;
        }

        public List<TransactionRiskPredictionEvaluation> EvaluateHighRiskOversampling()
        {
            var data = _datasetReader.Read("train.csv").ToList();
            var oversampledData = _riskDatasetGenerator.OversampleHighRisk(data);

            _datasetWriter.CsvDatasetWriter(
                "train_oversampled.csv",
                oversampledData);
            _modelTrainer.Train("train_oversampled.csv", "oversampled_model.zip");

            var evalData = _datasetReader.Read("evaluation.csv").ToList();


            var mlPrediction = _implementationResolverService.ResolveClassifier("ML");
            return evalData.Select(transaction =>
            {
                var rulesPrediction = mlPrediction.Predict(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                return new TransactionRiskPredictionEvaluation
                {
                    Actual = transaction.RiskLevel,
                    Predicted = rulesPrediction
                };

            }).ToList();

        }


        public SuspiciousTransactions FindSuspiciousTransactions()
        {
            var data = _datasetReader.Read("train.csv").ToList();

            var suspiciousMedium = data.Where(x => x.RiskLevel == "Medium"
            && x.RecentFailureCount >= 2 && x.IsHighRiskCountry).ToList();

            var suspiciousHigh = data.Where(x => x.RiskLevel == "High"
            && x.RecentFailureCount <= 1 && !x.IsHighRiskCountry).ToList();

            return new SuspiciousTransactions
            {
                SuspiciousMedium = new() { Records = suspiciousMedium, TotalCount = suspiciousMedium.Count },
                SuspiciousHigh = new() { Records = suspiciousHigh, TotalCount = suspiciousHigh.Count }
            };
        }

        public List<TransactionRiskPredictionEvaluation> EvaluateRandomHighRiskAugmentation()
        {
            var data = _datasetReader.Read("train.csv").ToList();
            var additionalHigh = new List<TransactionRiskData>();

            while (additionalHigh.Count < 162)
            {
                var transaction = _riskDatasetGenerator.GenerateRandomTransaction();

                if (transaction.RiskLevel == "High")
                {
                    additionalHigh.Add(transaction);
                }
            }

            var improvedTrainingData = data
                .Concat(additionalHigh)
                .ToList();

            _datasetWriter.CsvDatasetWriter(
                "train_oversampled_improvedv2.csv",
                improvedTrainingData);
            _modelTrainer.Train("train_oversampled_improvedv2.csv", "oversampled_model_improvedV2.zip");

            var evalData = _datasetReader.Read("evaluation.csv").ToList();


            var mlPrediction = _implementationResolverService.ResolveClassifier("ML");
            return evalData.Select(transaction =>
            {
                var rulesPrediction = mlPrediction.Predict(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                return new TransactionRiskPredictionEvaluation
                {
                    Actual = transaction.RiskLevel,
                    Predicted = rulesPrediction
                };

            }).ToList();

        }

        public List<TransactionRiskPredictionEvaluationWithRecord> FindHighRiskFalseNegatives()
        {
            var data = _datasetReader.Read("train.csv").ToList();

            var result = new List<TransactionRiskPredictionEvaluationWithRecord>();

            var mlPrediction = _implementationResolverService.ResolveClassifier("ML");
            var selectedData = data.Select(transaction =>
            {
                var rulesPrediction = mlPrediction.Predict(new TransactionRiskInput
                {
                    IsHighRiskCountry = transaction.IsHighRiskCountry,
                    Amount = transaction.Amount,
                    BeneficiaryAgeDays = transaction.BeneficiaryAgeDays,
                    CustomerTransactionCount24h = transaction.CustomerTransactionCount24h,
                    RecentFailureCount = transaction.RecentFailureCount,
                    TransactionHour = transaction.TransactionHour
                });

                if(transaction.RiskLevel == "High" && rulesPrediction != "High")
                {
                    result.Add(new TransactionRiskPredictionEvaluationWithRecord
                    {
                        Actual = transaction.RiskLevel,
                        Predicted = rulesPrediction,
                        Data = transaction
                    });
                }
                return new TransactionRiskPredictionEvaluation
                {
                    Actual = transaction.RiskLevel,
                    Predicted = rulesPrediction
                };

            }).ToList();

            return result;
        }

        // Chapter 6: 5,000 training and 1,000 held-out test records from one
        // fresh seeded generator, in natural proportions (no oversampling).
        public LargeDatasetResult GenerateLargeDataset()
        {
            const string trainFile = "chapter-06/train-5000.csv";
            const string testFile = "chapter-06/test-1000.csv";

            var generator = new RiskDatasetGenerator(seed: 42);
            var generated = generator.Generate(6000);

            var trainingData = generated.Take(5000).ToList();
            var testData = generated.Skip(5000).Take(1000).ToList();

            var trainingKeys = trainingData.Select(TransactionKey).ToHashSet();
            var crossDuplicates = testData.Select(TransactionKey).Distinct().Count(trainingKeys.Contains);

            if (crossDuplicates > 0)
                throw new InvalidOperationException(
                    $"{crossDuplicates} test transactions also appear in the training data.");

            _datasetWriter.CsvDatasetWriter(trainFile, trainingData);
            _datasetWriter.CsvDatasetWriter(testFile, testData);

            return new LargeDatasetResult
            {
                Train = Summarize(trainFile, trainingData),
                Test = Summarize(testFile, testData),
                CrossDatasetDuplicates = crossDuplicates
            };
        }

        // Chapter 6, step 2: split train-5000.csv into five stratified folds and
        // check the split. test-1000.csv is read only for the overlap check;
        // it takes no part in the split.
        public CrossValidationFoldsResult ValidateCrossValidationFolds()
        {
            const string trainFile = "chapter-06/train-5000.csv";
            const string testFile = "chapter-06/test-1000.csv";
            const int foldCount = 5;
            const int seed = 42;

            var training = _datasetReader.Read(trainFile).ToList();
            var before = training.ToList();

            var folds = _datasetSplitter.StratifiedKFold(training, foldCount, seed);
            var again = _datasetSplitter.StratifiedKFold(training, foldCount, seed);

            // Each record's position in the original list is its identity.
            var position = new Dictionary<TransactionRiskData, int>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < training.Count; i++) position[training[i]] = i;

            var seen = new int[training.Count];
            var foreign = 0;
            foreach (var record in folds.SelectMany(f => f))
            {
                if (position.TryGetValue(record, out var i)) seen[i]++;
                else foreign++;
            }

            var assignments = folds.Select(f => f.Select(r => position[r]).ToList()).ToList();
            var assignmentsAgain = again.Select(f => f.Select(r => position[r]).ToList()).ToList();

            var trainingKeys = training.Select(TransactionKey).ToHashSet();
            var trainTestDuplicates = _datasetReader.Read(testFile)
                .Select(TransactionKey).Distinct().Count(trainingKeys.Contains);

            return new CrossValidationFoldsResult
            {
                Dataset = trainFile,
                Seed = seed,
                FoldCount = foldCount,
                TotalRecords = training.Count,
                CombinedFoldRecords = folds.Sum(f => f.Count),
                Folds = folds.Select((f, i) => new FoldSummary
                {
                    Fold = i + 1,
                    Total = f.Count,
                    Low = f.Count(x => x.RiskLevel == "Low"),
                    Medium = f.Count(x => x.RiskLevel == "Medium"),
                    High = f.Count(x => x.RiskLevel == "High")
                }).ToList(),
                EveryRecordInExactlyOneFold = foreign == 0 && seen.All(n => n == 1),
                SameSeedGivesSameAssignments = assignments.Zip(assignmentsAgain).All(p => p.First.SequenceEqual(p.Second)),
                OriginalDatasetUnchanged = training.SequenceEqual(before, ReferenceEqualityComparer.Instance),
                TrainTestDuplicates = trainTestDuplicates
            };
        }

        // Chapter 6, step 3: SDCA Maximum Entropy baseline over the five
        // stratified folds of train-5000.csv. Default class prediction only.
        public CrossValidationEvaluationResult EvaluateSdcaCrossValidation() =>
            RunCrossValidation(
                "ML.NET SDCA Maximum Entropy",
                "evaluations/risk-classification/sdca-cross-validation-predictions.csv",
                CreateSdcaTrainer);

        // Chapter 6, step 4: the same SDCA trainer with the six raw features
        // plus four oracle-informed indicators. Only the features change.
        public CrossValidationEvaluationResult EvaluateSdcaEngineeredCrossValidation() =>
            RunCrossValidation(
                "ML.NET SDCA Maximum Entropy",
                "evaluations/risk-classification/sdca-engineered-cross-validation-predictions.csv",
                CreateSdcaTrainer,
                FeatureSet.Engineered);

        // Chapter 6, step 6: the step 3 baseline plus NormalizeMeanVariance.
        // Six original features; only the scaling changes.
        public CrossValidationEvaluationResult EvaluateSdcaNormalizedCrossValidation() =>
            RunCrossValidation(
                "ML.NET SDCA Maximum Entropy",
                "evaluations/risk-classification/sdca-normalized-cross-validation-predictions.csv",
                CreateSdcaTrainer,
                FeatureSet.Original,
                normalizeFeatures: true);

        // Chapter 6, step 5: step 4 plus NormalizeMeanVariance on the feature
        // vector, just before the trainer. Only the scaling changes.
        public CrossValidationEvaluationResult EvaluateSdcaEngineeredNormalizedCrossValidation() =>
            RunCrossValidation(
                "ML.NET SDCA Maximum Entropy",
                "evaluations/risk-classification/sdca-engineered-normalized-cross-validation-predictions.csv",
                CreateSdcaTrainer,
                FeatureSet.Engineered,
                normalizeFeatures: true);

        // Shared by all SDCA experiments, so their hyperparameters are the same.
        // One thread: multi-threaded SDCA gives a different model on every run,
        // even with a fixed seed.
        private static IEstimator<ITransformer> CreateSdcaTrainer(MLContext mlContext) =>
            mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                new SdcaMaximumEntropyMulticlassTrainer.Options
                {
                    LabelColumnName = "Label",
                    FeatureColumnName = "Features",
                    NumberOfThreads = 1
                });

        // Chapter 6: LightGBM multiclass on the same folds, features and metrics.
        // Only the trainer changes. The configuration is frozen: it passed the
        // reproducibility gate. LearningRate, NumberOfLeaves,
        // MinimumExampleCountPerLeaf, UseSoftmax and UseCategoricalSplit are the
        // values ML.NET auto-tuned for 4,000 rows; they are set here so a
        // different training size cannot change them. One thread and a fixed
        // seed make every run repeat exactly.
        public CrossValidationEvaluationResult EvaluateLightGbmCrossValidation() =>
            RunCrossValidation(
                "ML.NET LightGBM Multiclass",
                "evaluations/risk-classification/lightgbm-cross-validation-predictions.csv",
                CreateLightGbmTrainer);

        private static IEstimator<ITransformer> CreateLightGbmTrainer(MLContext mlContext) =>
            mlContext.MulticlassClassification.Trainers.LightGbm(
                new LightGbmMulticlassTrainer.Options
                {
                    LabelColumnName = "Label",
                    FeatureColumnName = "Features",
                    Seed = CrossValidationSeed,
                    NumberOfThreads = 1,
                    NumberOfIterations = 100,
                    LearningRate = 0.2,
                    NumberOfLeaves = 20,
                    MinimumExampleCountPerLeaf = 6,
                    UseSoftmax = false,
                    UseCategoricalSplit = false
                });

        private const int CrossValidationSeed = 42;

        private static readonly int[] LearningCurveSizes = [300, 600, 1200, 2400, 4000];

        private const double MaximumHighFlagRate = 0.40;

        // Chapter 6, step 8: threshold optimization for SDCA with engineered
        // features and normalization, by nested cross-validation.
        //
        // Outer loop: each of the 5 folds is locked in turn. The outer run is
        // the step 5 run: fold k is scored by a model trained on the other 4.
        // Inner loop: the other 4 folds run their own 4-fold cross-validation
        // (each inner model trains on 3 folds). Their out-of-fold High scores
        // never come from a model that saw fold k. The threshold is chosen on
        // those scores, then applied once to fold k. 5 + 5 x 4 = 25 models.
        public ThresholdEvaluationResult EvaluateSdcaNestedThreshold() =>
            RunNestedThreshold(SdcaCandidate, ReadTrainingFolds());

        private const string Chapter06TrainFile = "chapter-06/train-5000.csv";
        private const string Chapter06TestFile = "chapter-06/test-1000.csv";

        // The same five stratified folds as steps 2-8. test-1000.csv is not read.
        private List<List<TransactionRiskData>> ReadTrainingFolds() =>
            _datasetSplitter.StratifiedKFold(_datasetReader.Read(Chapter06TrainFile).ToList(), foldCount: 5, seed: CrossValidationSeed);

        // A model family with its features and scaling, as compared in steps 7-9.
        private sealed record ModelCandidate(
            string Name, string Key, Func<MLContext, IEstimator<ITransformer>> CreateTrainer,
            FeatureSet FeatureSet, bool NormalizeFeatures, string TrainerOptions);

        private static readonly ModelCandidate SdcaCandidate = new(
            "ML.NET SDCA Maximum Entropy", "sdca-engineered-normalized", CreateSdcaTrainer,
            FeatureSet.Engineered, NormalizeFeatures: true,
            "SdcaMaximumEntropy: NumberOfThreads 1, other options ML.NET defaults");

        private static readonly ModelCandidate LightGbmCandidate = new(
            "ML.NET LightGBM Multiclass", "lightgbm", CreateLightGbmTrainer,
            FeatureSet.Original, NormalizeFeatures: false,
            "LightGbm: Seed 42, NumberOfThreads 1, NumberOfIterations 100, LearningRate 0.2, NumberOfLeaves 20, " +
            "MinimumExampleCountPerLeaf 6, UseSoftmax false, UseCategoricalSplit false");

        private ThresholdEvaluationResult RunNestedThreshold(ModelCandidate candidate, List<List<TransactionRiskData>> folds)
        {
            var outer = _crossValidationEvaluator.Evaluate(
                candidate.Name, folds, candidate.CreateTrainer, CrossValidationSeed, candidate.FeatureSet, candidate.NormalizeFeatures);

            var foldResults = new List<NestedThresholdFold>();
            var gradedOuter = new List<(bool IsHigh, bool Flagged)>();

            for (var k = 0; k < folds.Count; k++)
            {
                // Inner cross-validation on the 4 folds that are not locked.
                var innerFolds = folds.Where((_, i) => i != k).ToList();
                var inner = _crossValidationEvaluator.Evaluate(
                    candidate.Name, innerFolds, candidate.CreateTrainer, CrossValidationSeed, candidate.FeatureSet, candidate.NormalizeFeatures);

                // Choose the threshold from the inner scores only.
                var chosen = ThresholdOptimizer.Select(HighScores(inner.Predictions), MaximumHighFlagRate);
                var threshold = chosen.Threshold!.Value;

                // Apply it once to the locked fold.
                var lockedFold = HighScores(outer.Predictions.Where(p => p.Fold == k + 1));
                var graded = lockedFold.Select(r => (r.IsHigh, Flagged: r.ScoreHigh >= threshold)).ToList();
                gradedOuter.AddRange(graded);

                foldResults.Add(new NestedThresholdFold
                {
                    Fold = k + 1,
                    InnerRecords = inner.OutOfFoldPredictionCount,
                    InnerTrainingRecords = inner.Folds.Select(f => f.TrainingRecords).ToList(),
                    OuterTrainingRecords = outer.Folds[k].TrainingRecords,
                    SelectedThreshold = threshold,
                    Inner = chosen,
                    Outer = ThresholdOptimizer.Metrics(graded, threshold),
                    OuterDefaultRule = ThresholdOptimizer.Metrics(DefaultRuleDecisions(outer.Predictions.Where(p => p.Fold == k + 1)))
                });
            }

            // Exploration on all 5,000 outer predictions. Choosing and grading
            // on the same records is optimistic, so it is reported separately.
            var allScores = HighScores(outer.Predictions);
            var sweep = ThresholdOptimizer.Sweep(allScores);
            var sweepFile = $"evaluations/risk-classification/{candidate.Key}-threshold-sweep.csv";
            _datasetWriter.CsvThresholdSweepWriter(sweepFile, sweep);

            var costRatioThreshold = ClassificationMetricsCalculator.FalsePositiveCost
                / (double)(ClassificationMetricsCalculator.FalsePositiveCost + ClassificationMetricsCalculator.FalseNegativeCost);
            double[] grid = [0.01, 0.02, 0.03, 0.05, 0.075, 0.10, 0.15, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90];

            return new ThresholdEvaluationResult
            {
                Model = candidate.Name,
                FeatureSet = outer.FeatureSet,
                FeatureScaling = outer.FeatureScaling,
                Dataset = Chapter06TrainFile,
                Seed = CrossValidationSeed,
                DecisionRule = "flag High when ScoreHigh >= threshold; ScoreHigh is read by class name",
                SelectionRule = "lowest business cost on the inner out-of-fold scores, flag rate <= 40%; ties keep the higher threshold",
                MaximumFlagRate = MaximumHighFlagRate,
                CostRatioThreshold = costRatioThreshold,
                NestedAggregate = ThresholdOptimizer.Metrics(gradedOuter),
                Folds = foldResults,
                DefaultRule = ThresholdOptimizer.Metrics(DefaultRuleDecisions(outer.Predictions)),
                CostRatioRule = ThresholdOptimizer.Evaluate(allScores, costRatioThreshold),
                InSampleBest = ThresholdOptimizer.Select(allScores, MaximumHighFlagRate),
                InSampleGrid = grid.Select(t => ThresholdOptimizer.Evaluate(allScores, t)).ToList(),
                SweepFile = sweepFile
            };
        }

        private const string FrozenModelFile = "Models/chapter-06-risk-classifier.zip";
        private const string FrozenSpecFile = "evaluations/risk-classification/chapter-06-frozen-model.json";
        private const string FinalTestFile = "evaluations/risk-classification/chapter-06-final-test-results.json";

        // Chapter 6, step 9: model selection.
        // 1. Both candidates run the same nested threshold protocol on the same
        //    outer folds. The lower nested business cost wins.
        // 2. The deployment threshold is chosen on the winner's 5,000
        //    out-of-fold training scores (training data only).
        // 3. The winner is trained on all 5,000 records with the same pipeline
        //    and saved. The model, threshold and hash are frozen in a JSON file
        //    before the test set is read. test-1000.csv is not read here.
        public ModelSelectionResult SelectFinalModel()
        {
            if (_finalModelStore.Exists(FinalTestFile))
                throw new InvalidOperationException("The test set was already used; the frozen model must not change.");

            var folds = ReadTrainingFolds();
            var results = new[] { SdcaCandidate, LightGbmCandidate }
                .Select(candidate => (Candidate: candidate, Result: RunNestedThreshold(candidate, folds)))
                .ToList();

            // Lowest nested cost; on a tie, higher recall, then lower flag rate.
            var (winner, winnerResult) = results
                .OrderBy(r => r.Result.NestedAggregate.BusinessCost)
                .ThenByDescending(r => r.Result.NestedAggregate.HighRecall)
                .ThenBy(r => r.Result.NestedAggregate.HighFlagRate)
                .First();

            // Train the winner on all 5,000 records, exactly as in each fold.
            var training = folds.SelectMany(f => f).ToList();
            var mlContext = new MLContext(seed: CrossValidationSeed);
            var trainingView = mlContext.Data.ShuffleRows(mlContext.Data.LoadFromEnumerable(training), seed: CrossValidationSeed);
            var model = CrossValidationEvaluator
                .BuildPipeline(mlContext, winner.CreateTrainer(mlContext), winner.FeatureSet, winner.NormalizeFeatures)
                .Fit(trainingView);
            var modelHash = _finalModelStore.SaveModel(mlContext, model, trainingView.Schema, FrozenModelFile);

            var frozen = new FrozenModelSpec
            {
                Model = winner.Name,
                FeatureSet = winner.FeatureSet.ToString(),
                Features = CrossValidationEvaluator.FeatureColumns(winner.FeatureSet).ToList(),
                FeatureScaling = winner.NormalizeFeatures
                    ? "NormalizeMeanVariance, fitted on the 5,000 training records"
                    : "none",
                TrainingFile = Chapter06TrainFile,
                TrainingRecords = training.Count,
                Seed = CrossValidationSeed,
                DeploymentThreshold = winnerResult.InSampleBest.Threshold!.Value,
                ThresholdSelection = "lowest business cost on the winner's 5,000 out-of-fold training scores, flag rate <= 40%; ties keep the higher threshold",
                ThresholdOnTrainingOutOfFold = winnerResult.InSampleBest,
                MaximumFlagRate = MaximumHighFlagRate,
                NestedEstimate = winnerResult.NestedAggregate,
                ModelFile = FrozenModelFile,
                ModelContentSha256 = modelHash,
                TestSetUsed = false
            };
            _finalModelStore.WriteJson(FrozenSpecFile, frozen, overwrite: true);

            return new ModelSelectionResult
            {
                SelectionRule = "lowest nested outer-fold business cost; ties: higher High recall, then lower flag rate",
                Candidates = results.Select(r => r.Result).ToList(),
                Winner = winner.Name,
                Frozen = frozen
            };
        }

        // Chapter 6, step 9: the one evaluation on test-1000.csv. It loads the
        // frozen model, checks its hash, and applies the frozen threshold.
        // It refuses to run a second time.
        public FinalTestResult EvaluateFinalModelOnTestSet()
        {
            if (_finalModelStore.Exists(FinalTestFile))
                throw new InvalidOperationException($"{FinalTestFile} exists: the test set has already been used once.");

            var frozen = _finalModelStore.ReadJson<FrozenModelSpec>(FrozenSpecFile);
            var mlContext = new MLContext(seed: frozen.Seed);
            var model = _finalModelStore.LoadModel(mlContext, frozen.ModelFile, frozen.ModelContentSha256);

            var test = _datasetReader.Read(Chapter06TestFile).ToList();
            var scored = model.Transform(mlContext.Data.LoadFromEnumerable(test));
            var scoreOrdering = CrossValidationEvaluator.ReadScoreOrdering(scored);
            var predictions = mlContext.Data
                .CreateEnumerable<TransactionRiskPrediction>(scored, reuseRowObject: false)
                .Select((prediction, i) => new OutOfFoldPrediction
                {
                    Actual = test[i].RiskLevel,
                    Predicted = prediction.RiskLevel,
                    Scores = scoreOrdering.Select((level, slot) => (level, slot))
                        .ToDictionary(x => x.level, x => prediction.Score[x.slot])
                })
                .ToList();

            if (predictions.Count != test.Count)
                throw new InvalidOperationException($"{predictions.Count} predictions for {test.Count} test records.");

            var thresholdMetrics = ThresholdOptimizer.Evaluate(HighScores(predictions), frozen.DeploymentThreshold);
            frozen.TestSetUsed = true;

            var result = new FinalTestResult
            {
                Frozen = frozen,
                TestFile = Chapter06TestFile,
                TestRecords = test.Count,
                ScoreOrdering = scoreOrdering,
                Threshold = thresholdMetrics,
                DefaultRule = ThresholdOptimizer.Metrics(DefaultRuleDecisions(predictions)),
                ThreeClassDefaultRule = ClassificationMetricsCalculator.Calculate(
                    predictions.Select(p => (p.Actual, p.Predicted)).ToList()),
                FlagRateWithinLimit = thresholdMetrics.HighFlagRate <= frozen.MaximumFlagRate
            };

            // Written once; a second run fails at the check above.
            _finalModelStore.WriteJson(FinalTestFile, result, overwrite: false);
            return result;
        }

        private static List<(bool IsHigh, float ScoreHigh)> HighScores(IEnumerable<OutOfFoldPrediction> predictions) =>
            predictions.Select(p => (p.Actual == "High", p.Scores["High"])).ToList();

        private static List<(bool IsHigh, bool Flagged)> DefaultRuleDecisions(IEnumerable<OutOfFoldPrediction> predictions) =>
            predictions.Select(p => (p.Actual == "High", p.Predicted == "High")).ToList();

        // Chapter 6, step 7: learning curves for the two strongest candidates.
        // Each fold's 4,000 training records are cut to nested stratified
        // subsets; every subset model is scored on that fold's same 1,000
        // validation records. The 4,000 point is the full step 5 / LightGBM run.
        public LearningCurveResult EvaluateLearningCurves()
        {
            const string trainFile = "chapter-06/train-5000.csv";

            // The same five stratified folds as steps 2-6. test-1000.csv is not read.
            var training = _datasetReader.Read(trainFile).ToList();
            var folds = _datasetSplitter.StratifiedKFold(training, foldCount: 5, seed: CrossValidationSeed);

            var candidates = new (string Model, Func<MLContext, IEstimator<ITransformer>> CreateTrainer, FeatureSet FeatureSet, bool Normalize)[]
            {
                ("ML.NET SDCA Maximum Entropy", CreateSdcaTrainer, FeatureSet.Engineered, true),
                ("ML.NET LightGBM Multiclass", CreateLightGbmTrainer, FeatureSet.Original, false)
            };

            var points = new List<LearningCurvePoint>();

            foreach (var size in LearningCurveSizes)
            {
                foreach (var candidate in candidates)
                {
                    var run = _crossValidationEvaluator.Evaluate(
                        candidate.Model, folds, candidate.CreateTrainer, CrossValidationSeed,
                        candidate.FeatureSet, candidate.Normalize, trainingSize: size);

                    points.Add(new LearningCurvePoint
                    {
                        Model = run.Model,
                        FeatureSet = run.FeatureSet,
                        FeatureScaling = run.FeatureScaling,
                        TrainingSize = size,
                        ScoreOrderingSameInEveryFold = run.ScoreOrderingSameInEveryFold,
                        Aggregate = run.Aggregate,
                        Folds = run.Folds
                    });
                }
            }

            return new LearningCurveResult
            {
                Dataset = trainFile,
                Seed = CrossValidationSeed,
                FoldCount = folds.Count,
                SubsetRule = "DatasetSplitter.NestedStratifiedSubset per fold, seed 42: nested and stratified; 4,000 is the full fold training set",
                PredictionRule = "default class prediction (highest score); no threshold tuning",
                TrainingSizes = [.. LearningCurveSizes],
                Points = points
            };
        }

        // Shared by every model, so the folds, features and metrics stay the same.
        private CrossValidationEvaluationResult RunCrossValidation(
            string modelName,
            string predictionsFile,
            Func<MLContext, IEstimator<ITransformer>> createTrainer,
            FeatureSet featureSet = FeatureSet.Original,
            bool normalizeFeatures = false)
        {
            const string trainFile = "chapter-06/train-5000.csv";

            // The same five stratified folds as step 2. test-1000.csv is not read.
            var training = _datasetReader.Read(trainFile).ToList();
            var folds = _datasetSplitter.StratifiedKFold(training, foldCount: 5, seed: CrossValidationSeed);

            // createTrainer says which model to train. The evaluator calls it
            // once per fold, so each fold gets a new, untrained model.
            var result = _crossValidationEvaluator.Evaluate(
                modelName, folds, createTrainer, CrossValidationSeed, featureSet, normalizeFeatures);

            // Save all 5,000 out-of-fold predictions with their class scores,
            // for review and for the threshold step.
            result.Dataset = trainFile;
            result.PredictionsFile = predictionsFile;
            _datasetWriter.CsvPredictionWriter(predictionsFile, result.Predictions, result.ScoreOrdering);

            return result;
        }

        // A transaction's identity is its six features, formatted as they are
        // written to CSV. The label is not part of the identity.
        private static string TransactionKey(TransactionRiskData t) => string.Join(",",
            t.Amount.ToString(CultureInfo.InvariantCulture),
            t.TransactionHour.ToString(CultureInfo.InvariantCulture),
            t.CustomerTransactionCount24h.ToString(CultureInfo.InvariantCulture),
            t.RecentFailureCount.ToString(CultureInfo.InvariantCulture),
            t.BeneficiaryAgeDays.ToString(CultureInfo.InvariantCulture),
            t.IsHighRiskCountry);

        private static DatasetSummary Summarize(string file, List<TransactionRiskData> data) => new()
        {
            File = file,
            Total = data.Count,
            Low = data.Count(x => x.RiskLevel == "Low"),
            Medium = data.Count(x => x.RiskLevel == "Medium"),
            High = data.Count(x => x.RiskLevel == "High"),
            DuplicateRecords = data.Count - data.Select(TransactionKey).Distinct().Count()
        };
    }
}
