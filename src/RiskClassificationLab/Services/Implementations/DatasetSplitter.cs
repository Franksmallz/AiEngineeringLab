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

        // Stratified k-fold: each class is shuffled with a fixed seed, then dealt
        // to the folds round-robin. The fold counter carries over from one class
        // to the next, so the remainders spread out and the fold totals stay even.
        // The input list is not modified; the folds hold the same record instances.
        public List<List<TransactionRiskData>> StratifiedKFold(
            IReadOnlyList<TransactionRiskData> data, int foldCount = 5, int seed = 42)
        {
            if (foldCount < 2)
                throw new ArgumentOutOfRangeException(nameof(foldCount), "At least two folds are needed.");

            var random = new Random(seed);
            var folds = Enumerable.Range(0, foldCount).Select(_ => new List<TransactionRiskData>()).ToList();
            var next = 0;

            foreach (var group in data.GroupBy(x => x.RiskLevel).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var items = group.ToList();

                // Fisher-Yates shuffle of the copy
                for (var i = items.Count - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (items[i], items[j]) = (items[j], items[i]);
                }

                foreach (var item in items)
                {
                    folds[next].Add(item);
                    next = (next + 1) % foldCount;
                }
            }

            return folds;
        }

        // A stratified training subset for learning curves. Each class is shuffled
        // with a fixed seed. Each record then gets a position (rank + 0.5) / class
        // size, so every class is spread evenly from 0 to 1. The records are put in
        // order of position, and the subset is the first `size` records. So a
        // smaller subset is always contained in a larger one (nested), and every
        // subset keeps the class mix. The full size returns the data unchanged.
        public List<TransactionRiskData> NestedStratifiedSubset(
            IReadOnlyList<TransactionRiskData> data, int size, int seed = 42)
        {
            if (size < 1 || size > data.Count)
                throw new ArgumentOutOfRangeException(nameof(size), $"Size must be from 1 to {data.Count}.");

            if (size == data.Count)
                return data.ToList();

            var random = new Random(seed);
            var positioned = new List<(double Position, int ClassIndex, TransactionRiskData Item)>();
            var classIndex = 0;

            foreach (var group in data.GroupBy(x => x.RiskLevel).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var items = group.ToList();

                // Fisher-Yates shuffle of the copy
                for (var i = items.Count - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (items[i], items[j]) = (items[j], items[i]);
                }

                for (var i = 0; i < items.Count; i++)
                    positioned.Add(((i + 0.5) / items.Count, classIndex, items[i]));

                classIndex++;
            }

            return positioned
                .OrderBy(x => x.Position)
                .ThenBy(x => x.ClassIndex)
                .Take(size)
                .Select(x => x.Item)
                .ToList();
        }

        public (List<TransactionRiskData> Train, List<TransactionRiskData> Validation)
            GetFoldSplit(List<List<TransactionRiskData>> folds, int validationFold)
        {
            if (validationFold < 0 || validationFold >= folds.Count)
                throw new ArgumentOutOfRangeException(nameof(validationFold));

            var train = folds.Where((_, i) => i != validationFold).SelectMany(f => f).ToList();
            var validation = folds[validationFold].ToList();

            return (train, validation);
        }
    }
}
