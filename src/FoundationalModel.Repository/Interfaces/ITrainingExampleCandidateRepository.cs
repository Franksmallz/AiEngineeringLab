using FoundationalModel.Models.Entities;

namespace FoundationalModel.Repository.Interfaces
{
    public interface ITrainingExampleCandidateRepository : IAutoDependencyRepository
    {
        Task SaveAsync(TrainingExampleCandidate candidate, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<TrainingExampleCandidate>>GetAllAsync(CancellationToken cancellationToken = default);
    }
}
