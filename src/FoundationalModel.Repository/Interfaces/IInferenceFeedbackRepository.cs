using FoundationalModel.Models.Entities;

namespace FoundationalModel.Repository.Interfaces
{
    public interface IInferenceFeedbackRepository : IAutoDependencyRepository
    {
        Task SaveAsync(InferenceFeedback feedback, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<InferenceFeedback>> GetByInferenceIdAsync(string inferenceId, CancellationToken cancellationToken = default);
    }
}
