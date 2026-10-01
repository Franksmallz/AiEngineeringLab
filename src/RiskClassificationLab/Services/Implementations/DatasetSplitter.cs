using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Services.Implementations
{
    public class DatasetSplitter : IDatasetSplitter
    {
        public (List<TransactionRiskData> Train, List<TransactionRiskData> Evaluation) 
            Split(List<TransactionRiskData> data, double trainingPercentage = 0.75)
        {
            var train = new List<TransactionRiskData>();
            var evaluation = new List<TransactionRiskData>();

            foreach (var group in data.GroupBy(x => x.RiskLevel))
            {
                var items = group.ToList();

                var trainingCount = (int)Math.Round(items.Count * trainingPercentage);

                train.AddRange(items.Take(trainingCount));
                evaluation.AddRange(items.Skip(trainingCount));
            }

            return (train, evaluation);
        }
    }
}
