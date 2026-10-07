using Microsoft.AspNetCore.Mvc;
using RiskClassificationLab.Controllers;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Tests;

public sealed class RiskControllerTests
{
    [Fact]
    public void Renamed_actions_expose_matching_route_templates()
    {
        var routes = typeof(RiskController)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
                .Cast<HttpPostAttribute>()
                .Select(attribute => (method.Name, attribute.Template)))
            .ToDictionary(x => x.Name, x => x.Template);

        Assert.Equal("/risk/profile-dataset", routes[nameof(RiskController.ProfileRiskDataset)]);
        Assert.Equal("/risk/evaluate-high-risk-oversampling", routes[nameof(RiskController.EvaluateHighRiskOversampling)]);
        Assert.Equal("/risk/suspicious-transactions", routes[nameof(RiskController.FindSuspiciousTransactions)]);
        Assert.Equal("/risk/evaluate-random-high-risk-augmentation", routes[nameof(RiskController.EvaluateRandomHighRiskAugmentation)]);
        Assert.Equal("/risk/high-risk-false-negatives", routes[nameof(RiskController.FindHighRiskFalseNegatives)]);
    }

    [Fact]
    public async Task Endpoints_return_the_corresponding_service_results()
    {
        var expectedData = new TransactionRiskDataResult();
        var expectedEvaluation = new TransactionRiskEvaluationResult();
        var expectedEdgeCases = new EdgeCaseEvaluationResult();
        var expectedPrediction = new TransactionRiskPrediction { RiskLevel = "High", Score = [0.1f, 0.9f] };
        var expectedThresholds = new List<MetricsThreshold> { new() { Threshold = 0.9f } };
        var expectedScores = new[] { 0.2f, 0.9f };
        var expectedProfile = new List<ClassProfile>();
        var expectedOversampling = new List<TransactionRiskPredictionEvaluation>();
        var expectedSuspicious = new SuspiciousTransactions();
        var expectedAugmentation = new List<TransactionRiskPredictionEvaluation>();
        var expectedFalseNegatives = new List<TransactionRiskPredictionEvaluationWithRecord>();
        var service = new StubRiskService(expectedData, expectedEvaluation, expectedEdgeCases, expectedPrediction, expectedThresholds, expectedScores, expectedProfile, expectedOversampling, expectedSuspicious, expectedAugmentation, expectedFalseNegatives);
        var controller = new RiskController(service);

        var generate = Assert.IsType<OkObjectResult>(await controller.Generate(CancellationToken.None));
        var predict = Assert.IsType<OkObjectResult>(await controller.Predict(new TransactionRiskInput()));
        var evaluate = Assert.IsType<OkObjectResult>(await controller.Evaluate());
        var train = Assert.IsType<OkObjectResult>(await controller.Train());
        var mlPredict = Assert.IsType<OkObjectResult>(await controller.MLPredict(new TransactionRiskInput()));
        var edgeCases = Assert.IsType<OkObjectResult>(await controller.EvaluateEdgeCases());
        var prediction = Assert.IsType<OkObjectResult>(await controller.PredictWithScores(new TransactionRiskInput()));
        var thresholds = Assert.IsType<OkObjectResult>(await controller.EvaluateWithScore());
        var scores = Assert.IsType<OkObjectResult>(await controller.HighRiskScores());
        var profile = Assert.IsType<OkObjectResult>(await controller.ProfileRiskDataset());
        var oversampling = Assert.IsType<OkObjectResult>(await controller.EvaluateHighRiskOversampling());
        var suspicious = Assert.IsType<OkObjectResult>(await controller.FindSuspiciousTransactions());
        var augmentation = Assert.IsType<OkObjectResult>(await controller.EvaluateRandomHighRiskAugmentation());
        var falseNegatives = Assert.IsType<OkObjectResult>(await controller.FindHighRiskFalseNegatives());

        Assert.Same(expectedData, generate.Value);
        Assert.Equal("Low", predict.Value);
        Assert.Same(expectedEvaluation, evaluate.Value);
        Assert.Equal("trained", train.Value);
        Assert.Equal("High", mlPredict.Value);
        Assert.Same(expectedEdgeCases, edgeCases.Value);
        Assert.Same(expectedPrediction, prediction.Value);
        Assert.Same(expectedThresholds, thresholds.Value);
        Assert.Same(expectedScores, scores.Value);
        Assert.Same(expectedProfile, profile.Value);
        Assert.Same(expectedOversampling, oversampling.Value);
        Assert.Same(expectedSuspicious, suspicious.Value);
        Assert.Same(expectedAugmentation, augmentation.Value);
        Assert.Same(expectedFalseNegatives, falseNegatives.Value);
    }

    private sealed class StubRiskService(
        TransactionRiskDataResult data,
        TransactionRiskEvaluationResult evaluation,
        EdgeCaseEvaluationResult edgeCases,
        TransactionRiskPrediction prediction,
        List<MetricsThreshold> thresholds,
        float[] scores,
        List<ClassProfile> profile,
        List<TransactionRiskPredictionEvaluation> oversampling,
        SuspiciousTransactions suspicious,
        List<TransactionRiskPredictionEvaluation> augmentation,
        List<TransactionRiskPredictionEvaluationWithRecord> falseNegatives) : IRiskService
    {
        public TransactionRiskDataResult Generate() => data;
        public string Predict(TransactionRiskInput transaction) => "Low";
        public TransactionRiskEvaluationResult Evaluate() => evaluation;
        public string Train() => "trained";
        public string MLPredict(TransactionRiskInput transaction) => "High";
        public EdgeCaseEvaluationResult EvaluateEdgeCases() => edgeCases;
        public TransactionRiskPrediction PredictWithScores(TransactionRiskInput input) => prediction;
        public List<MetricsThreshold> EvaluateWithScores() => thresholds;
        public float[] HighRiskScores() => scores;
        public List<ClassProfile> ProfileDataset() => profile;
        public List<TransactionRiskPredictionEvaluation> EvaluateHighRiskOversampling() => oversampling;
        public SuspiciousTransactions FindSuspiciousTransactions() => suspicious;
        public List<TransactionRiskPredictionEvaluation> EvaluateRandomHighRiskAugmentation() => augmentation;
        public List<TransactionRiskPredictionEvaluationWithRecord> FindHighRiskFalseNegatives() => falseNegatives;
    }
}
