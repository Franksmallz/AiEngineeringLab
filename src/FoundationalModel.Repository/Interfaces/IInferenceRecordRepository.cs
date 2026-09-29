using FoundationalModel.Models.Entities;

namespace FoundationalModel.Repository.Interfaces
{
    public interface IInferenceRecordRepository : IAutoDependencyRepository
    {
        Task SaveAsync(InferenceRecord record, CancellationToken cancellationToken = default);
        Task <InferenceRecord?>GetAsync(string inferenceId, CancellationToken cancellationToken = default);
    }
}
