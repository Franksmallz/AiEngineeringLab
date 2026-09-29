using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using System.Collections.Concurrent;

namespace FoundationalModel.Repository.Implementations
{
    public class TrainingExampleCandidateRepository : ITrainingExampleCandidateRepository
    {
        private readonly ConcurrentDictionary<string, TrainingExampleCandidate> _candidates = new();
        public Task<IReadOnlyCollection<TrainingExampleCandidate>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyCollection<TrainingExampleCandidate>)_candidates.Values.ToList());
        }

        public Task SaveAsync(TrainingExampleCandidate candidate, CancellationToken cancellationToken = default)
        {
            _candidates[candidate.CandidateId] = candidate;
            return Task.CompletedTask;
        }
    }
}
