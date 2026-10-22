namespace RiskClassificationLab.Enums
{
    public enum FeatureSet
    {
        // The six raw features.
        Original,

        // The six raw features plus four oracle-informed indicators that copy
        // the thresholds used by RiskDatasetGenerator.DetermineRiskLevel.
        Engineered
    }
}
