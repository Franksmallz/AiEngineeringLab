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

        [HttpPost("/rules/evaluate")]
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
    }

}
