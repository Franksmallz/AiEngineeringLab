using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IDatasetSplitter : IAutoDependencyService
    {
        public (List<TransactionRiskData> Train, List<TransactionRiskData> Evaluation)
            Split(List<TransactionRiskData> data, double trainingPercentage = 0.75);

        public List<List<TransactionRiskData>> StratifiedKFold(
            IReadOnlyList<TransactionRiskData> data, int foldCount = 5, int seed = 42);

        public List<TransactionRiskData> NestedStratifiedSubset(
            IReadOnlyList<TransactionRiskData> data, int size, int seed = 42);

        public (List<TransactionRiskData> Train, List<TransactionRiskData> Validation)
            GetFoldSplit(List<List<TransactionRiskData>> folds, int validationFold);
    }
}
