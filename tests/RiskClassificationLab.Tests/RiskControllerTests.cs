using Microsoft.AspNetCore.Mvc;
using RiskClassificationLab.Controllers;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Tests;

public sealed class RiskControllerTests
{
    [Fact]
    public async Task Endpoints_return_the_corresponding_service_results()
    {
        var expectedData = new TransactionRiskDataResult();
        var expectedEvaluation = new TransactionRiskEvaluationResult();
        var expectedEdgeCases = new EdgeCaseEvaluationResult();
        var expectedPrediction = new TransactionRiskPrediction { RiskLevel = "High", Score = [0.1f, 0.9f] };
        var expectedThresholds = new List<MetricsThreshold> { new() { Threshold = 0.9f } };
        var expectedScores = new[] { 0.2f, 0.9f };
        var service = new StubRiskService(expectedData, expectedEvaluation, expectedEdgeCases, expectedPrediction, expectedThresholds, expectedScores);
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

        Assert.Same(expectedData, generate.Value);
        Assert.Equal("Low", predict.Value);
        Assert.Same(expectedEvaluation, evaluate.Value);
        Assert.Equal("trained", train.Value);
        Assert.Equal("High", mlPredict.Value);
        Assert.Same(expectedEdgeCases, edgeCases.Value);
        Assert.Same(expectedPrediction, prediction.Value);
        Assert.Same(expectedThresholds, thresholds.Value);
        Assert.Same(expectedScores, scores.Value);
    }

    private sealed class StubRiskService(
        TransactionRiskDataResult data,
        TransactionRiskEvaluationResult evaluation,
        EdgeCaseEvaluationResult edgeCases,
        TransactionRiskPrediction prediction,
        List<MetricsThreshold> thresholds,
        float[] scores) : IRiskService
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
    }
}
