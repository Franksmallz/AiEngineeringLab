using Microsoft.ML.Transforms;

namespace RiskClassificationLab.Models
{
    // Lets ML.NET save and load the mapping: a saved model stores the
    // contract name, and the loader finds this factory by that name.
    [CustomMappingFactoryAttribute(EngineeredFeatures.ContractName)]
    public class EngineeredFeaturesFactory : CustomMappingFactory<EngineeredFeatureInput, EngineeredFeatures>
    {
        public override Action<EngineeredFeatureInput, EngineeredFeatures> GetMapping() => EngineeredFeatures.Map;
    }

    // Input columns for the engineered-feature mapping.
    public class EngineeredFeatureInput
    {
        public float Amount { get; set; }
        public float TransactionHour { get; set; }
        public float BeneficiaryAgeDays { get; set; }
    }

    // Four 0/1 indicators. The thresholds are the ones the synthetic generator
    // uses to build the risk score (an oracle-informed experiment).
    public class EngineeredFeatures
    {
        public const string ContractName = "EngineeredFeatures";

        public const float NewBeneficiaryMaxAgeDays = 7;     // BeneficiaryAgeDays < 7
        public const float LargeTransactionAmount = 3000;    // Amount > 3000
        public const float LateNightLastHour = 4;            // TransactionHour <= 4

        public float IsNewBeneficiary { get; set; }
        public float IsLargeTransaction { get; set; }
        public float NewBeneficiaryLargeAmount { get; set; }
        public float IsLateNight { get; set; }

        public static void Map(EngineeredFeatureInput input, EngineeredFeatures output)
        {
            var isNew = input.BeneficiaryAgeDays < NewBeneficiaryMaxAgeDays;
            var isLarge = input.Amount > LargeTransactionAmount;

            output.IsNewBeneficiary = isNew ? 1f : 0f;
            output.IsLargeTransaction = isLarge ? 1f : 0f;
            output.NewBeneficiaryLargeAmount = isNew && isLarge ? 1f : 0f;
            output.IsLateNight = input.TransactionHour <= LateNightLastHour ? 1f : 0f;
        }
    }
}
