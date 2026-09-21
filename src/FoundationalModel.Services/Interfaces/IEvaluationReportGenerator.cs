using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IEvaluationReportGenerator : IAutoDependencyService
    {
        public string Genrate(ManualEvaluationSummary v1, ManualEvaluationSummary v2, int v1SchemaCompliant, int v2SchemaCompliant);
    }
}
