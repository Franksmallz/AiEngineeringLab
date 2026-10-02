using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IDatasetWriter  : IAutoDependencyService
    {
        void CsvDatasetWriter(string filename, List<TransactionRiskData> data);
    }
}
