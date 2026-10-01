using RiskClassificationLab.Models;

namespace RiskClassificationLab.Services.Interfaces
{
    public interface IDatasetReader : IAutoDependencyService
    {
        IEnumerable<TransactionRiskData> Read(string filename);
    }
}
