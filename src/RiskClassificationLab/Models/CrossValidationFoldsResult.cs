namespace RiskClassificationLab.Models
{
    public class CrossValidationFoldsResult
    {
        public string Dataset { get; set; } = string.Empty;
        public int Seed { get; set; }
        public int FoldCount { get; set; }
        public int TotalRecords { get; set; }
        public int CombinedFoldRecords { get; set; }
        public List<FoldSummary> Folds { get; set; } = [];
        public bool EveryRecordInExactlyOneFold { get; set; }
        public bool SameSeedGivesSameAssignments { get; set; }
        public bool OriginalDatasetUnchanged { get; set; }
        public int TrainTestDuplicates { get; set; }
    }

    public class FoldSummary
    {
        public int Fold { get; set; }
        public int Total { get; set; }
        public int Low { get; set; }
        public int Medium { get; set; }
        public int High { get; set; }
    }
}
