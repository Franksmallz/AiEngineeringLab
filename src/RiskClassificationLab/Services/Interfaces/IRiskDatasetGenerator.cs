using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IRiskDatasetGenerator : IAutoDependencyService
    {
        public List<TransactionRiskData> Generate(int count);
        public List<TransactionRiskData> OversampleHighRisk(List<TransactionRiskData> trainingData);
        TransactionRiskData GenerateRandomTransaction();
    }
}
