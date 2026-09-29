using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FoundationalModel.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DatasetsController : ControllerBase
    {
        private readonly ITrainingDatasetManagement _trainingDatasetManagement;

        public DatasetsController(ITrainingDatasetManagement trainingDatasetManagement)
        {
            _trainingDatasetManagement = trainingDatasetManagement;
        }

        [HttpPost("/export/{version}")]
        public async Task<IActionResult> SubmitFeedback([FromQuery] string version, CancellationToken cancellationToken)
        {
            var result = await _trainingDatasetManagement.ExportAsync(version, "finetuning/data");
            return Ok(result);
        }
    }
}
