using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IDatasetProfiler : IAutoDependencyService
    {
        List<ClassProfile> ProfileDataset(List<TransactionRiskData> data);
    }
}
