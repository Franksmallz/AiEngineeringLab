using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IRiskDatasetGenerator : IAutoDependencyService
    {
        public List<TransactionRiskData> Generate(int count);
    }
}
