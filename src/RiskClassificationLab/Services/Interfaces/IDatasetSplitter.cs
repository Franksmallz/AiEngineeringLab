using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IDatasetSplitter : IAutoDependencyService
    {
        public (List<TransactionRiskData> Train, List<TransactionRiskData> Evaluation)
            Split(List<TransactionRiskData> data, double trainingPercentage = 0.75);
    }
}
