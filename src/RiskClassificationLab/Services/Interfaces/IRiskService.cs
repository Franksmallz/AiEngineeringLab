using RiskClassificationLab.Enums;
using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IRiskService : IAutoDependencyService
    {
        TransactionRiskDataResult Generate();
        string Predict(TransactionRiskInput transaction);
        TransactionRiskEvaluationResult Evaluate();
        string Train();
        string MLPredict(TransactionRiskInput transaction);
        EdgeCaseEvaluationResult EvaluateEdgeCases();
        TransactionRiskPrediction PredictWithScores(TransactionRiskInput input);
        List<MetricsThreshold> EvaluateWithScores();
        float[] HighRiskScores();
        List<ClassProfile> ProfileDataset();
        List<TransactionRiskPredictionEvaluation> EvaluateHighRiskOversampling();
        SuspiciousTransactions FindSuspiciousTransactions();
        List<TransactionRiskPredictionEvaluation> EvaluateRandomHighRiskAugmentation();
        List<TransactionRiskPredictionEvaluationWithRecord> FindHighRiskFalseNegatives();
        LargeDatasetResult GenerateLargeDataset();
        CrossValidationFoldsResult ValidateCrossValidationFolds();
        CrossValidationEvaluationResult EvaluateSdcaCrossValidation();
        CrossValidationEvaluationResult EvaluateLightGbmCrossValidation();
        CrossValidationEvaluationResult EvaluateSdcaEngineeredCrossValidation();
        CrossValidationEvaluationResult EvaluateSdcaEngineeredNormalizedCrossValidation();
        CrossValidationEvaluationResult EvaluateSdcaNormalizedCrossValidation();
        LearningCurveResult EvaluateLearningCurves();
        ThresholdEvaluationResult EvaluateSdcaNestedThreshold();
        ModelSelectionResult SelectFinalModel();
        FinalTestResult EvaluateFinalModelOnTestSet();
    }
}
