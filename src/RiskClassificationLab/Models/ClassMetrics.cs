namespace RiskClassificationLab.Models
{
    public class ClassMetrics
    {
        public string Class { get; set; } = string.Empty;
        public int TruePositive { get; set; }
        public int FalsePositive { get; set; }
        public int FalseNegative { get; set; }

        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1 { get; set; }
    }
}
