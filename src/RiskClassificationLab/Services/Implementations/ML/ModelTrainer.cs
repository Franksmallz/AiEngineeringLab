using Microsoft.ML;
using Microsoft.ML.Data;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using RiskClassificationLab.Services.Interfaces.ML;

namespace RiskClassificationLab.Services.Implementations.ML
{
    public class ModelTrainer : IModelTrainer
    {
        private readonly MLContext _mlContext = new(seed: 42);
        private readonly IPathResolver _pathResolver;

        public ModelTrainer(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public void Train(string filename, string Modelfilename)
        {
            var directory = _pathResolver.ResolveConfiguredPath("data/risk-classification");

            var data = _mlContext.Data.LoadFromTextFile<TransactionRiskData>(
           Path.Combine(directory, filename),
           hasHeader: true,
           separatorChar: ',');
            var pipeline = _mlContext.Transforms.Conversion
            .MapValueToKey(
            outputColumnName: "Label",
            inputColumnName: nameof(TransactionRiskData.RiskLevel))
            .Append(_mlContext.Transforms.Conversion.ConvertType(
            outputColumnName: "IsHighRiskCountryFloat",
            inputColumnName: nameof(TransactionRiskData.IsHighRiskCountry),
            outputKind: DataKind.Single))

        .Append(_mlContext.Transforms.Concatenate(
            "Features",
            nameof(TransactionRiskData.Amount),
            nameof(TransactionRiskData.TransactionHour),
            nameof(TransactionRiskData.CustomerTransactionCount24h),
            nameof(TransactionRiskData.RecentFailureCount),
            nameof(TransactionRiskData.BeneficiaryAgeDays),
            "IsHighRiskCountryFloat"
        ))
            .Append(_mlContext.MulticlassClassification.Trainers
            .SdcaMaximumEntropy(
            labelColumnName: "Label",
            featureColumnName: "Features"))
            .Append(_mlContext.Transforms.Conversion
            .MapKeyToValue(
                outputColumnName: "PredictedRiskLevel",
                inputColumnName: "PredictedLabel"));

            var model = pipeline.Fit(data);
            var modelDirectory = _pathResolver.ResolveConfiguredPath("models");
            var modelPath = Path.Combine(modelDirectory, Modelfilename);

            _mlContext.Model.Save(
            model,
            data.Schema,
            modelPath);
        }
    }
}
