namespace RiskClassificationLab.Models
{
    public class MetricsThreshold
    {
        public float Threshold { get; set; }
        public int TP { get; set; }
        public int FP { get; set; }
        public int FN { get; set; }
        public int TN { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1 { get; set; }
        public float BusinessCost { get; set; }
        public bool meetsConstraints { get; set; }
        public double flagRate { get; set; }
    }
}
