using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Repository.Interfaces;

namespace FoundationalModel.Services.Interfaces
{
    public interface ITrainingDatasetManagement : IAutoDependencyRepository
    {
        Task<TrainingDataVersion> ExportAsync(string version, string outputDirectory);
    }
}
