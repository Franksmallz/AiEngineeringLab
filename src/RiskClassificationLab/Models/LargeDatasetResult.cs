namespace RiskClassificationLab.Models
{
    public class LargeDatasetResult
    {
        public DatasetSummary Train { get; set; } = new();
        public DatasetSummary Test { get; set; } = new();
        public int CrossDatasetDuplicates { get; set; }
    }

    public class DatasetSummary
    {
        public string File { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Low { get; set; }
        public int Medium { get; set; }
        public int High { get; set; }
        public int DuplicateRecords { get; set; }
    }
}
