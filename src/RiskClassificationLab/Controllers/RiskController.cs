using Microsoft.AspNetCore.Mvc;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RiskController : ControllerBase
    {
        private readonly IRiskService _riskService;

        public RiskController(IRiskService riskService)
        {
            _riskService = riskService;
        }

        [HttpPost("/generate")]
        [Produces(typeof(TransactionRiskDataResult))]
        public async Task<IActionResult> Generate(CancellationToken cancellationToken)
        {
            var distribution = _riskService.Generate();
            return Ok(distribution);
        }

        [HttpPost("/rules")]
        [Produces(typeof(List<TransactionRiskData>))]
        public async Task<IActionResult> Predict(TransactionRiskInput transaction)
        {
            var prediction = _riskService.Predict(transaction);
            return Ok(prediction);
        }

        [HttpPost("/evaluate")]
        [Produces(typeof(TransactionRiskEvaluationResult))]
        public async Task<IActionResult> Evaluate()
        {
            var result = _riskService.Evaluate();
            return Ok(result);
        }

        [HttpPost("/ml/train")]
        [Produces(typeof(string))]
        public async Task<IActionResult> Train()
        {
            var result = _riskService.Train();
            return Ok(result);
        }

        [HttpPost("/ml/rules")]
        [Produces(typeof(string))]
        public async Task<IActionResult> MLPredict(TransactionRiskInput model)
        {
            var result = _riskService.MLPredict(model);
            return Ok(result);
        }

        [HttpPost("/edge-cases")]
        [Produces(typeof(EdgeCaseEvaluationResult))]
        public async Task<IActionResult> EvaluateEdgeCases()
        {
            var result = _riskService.EvaluateEdgeCases();
            return Ok(result);
        }

        [HttpPost("/risk/ml/scores")]
        [Produces(typeof(TransactionRiskPrediction))]
        public async Task<IActionResult> PredictWithScores(TransactionRiskInput input)
        {
            var result = _riskService.PredictWithScores(input);
            return Ok(result);
        }

        [HttpPost("/evaluate/scores")]
        public async Task<IActionResult> EvaluateWithScore()
        {
            var result = _riskService.EvaluateWithScores();
            return Ok(result);
        }

        [HttpPost("/risk/scores")]
        public async Task<IActionResult> HighRiskScores()
        {
            var result = _riskService.HighRiskScores();
            return Ok(result);
        }

        [HttpPost("/risk/profile-dataset")]
        public async Task<IActionResult> ProfileRiskDataset()
        {
            var result = _riskService.ProfileDataset();
            return Ok(result);
        }

        [HttpPost("/risk/evaluate-high-risk-oversampling")]
        public async Task<IActionResult> EvaluateHighRiskOversampling()
        {
            var result = _riskService.EvaluateHighRiskOversampling();
            return Ok(result);
        }

        [HttpPost("/risk/suspicious-transactions")]
        public async Task<IActionResult> FindSuspiciousTransactions()
        {
            var result = _riskService.FindSuspiciousTransactions();
            return Ok(result);
        }

        [HttpPost("/risk/evaluate-random-high-risk-augmentation")]
        public async Task<IActionResult> EvaluateRandomHighRiskAugmentation()
        {
            var result = _riskService.EvaluateRandomHighRiskAugmentation();
            return Ok(result);
        }

        [HttpPost("/risk/high-risk-false-negatives")]
        public async Task<IActionResult> FindHighRiskFalseNegatives()
        {
            var result = _riskService.FindHighRiskFalseNegatives();
            return Ok(result);
        }
    }

}
