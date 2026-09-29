using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using System.Collections.Concurrent;

namespace FoundationalModel.Repository.Implementations
{
    public class InferenceFeedbackRepository : IInferenceFeedbackRepository
    {
        private readonly ConcurrentDictionary<string, List<InferenceFeedback>> _feedback = new();
        public Task<IReadOnlyCollection<InferenceFeedback>> GetByInferenceIdAsync(string inferenceId, CancellationToken cancellationToken = default)
        {
            if(_feedback.TryGetValue(inferenceId, out var items))
            {
                lock (items)
                {
                    return Task.FromResult((IReadOnlyCollection<InferenceFeedback>)items.ToList());
                }
            }
            return Task.FromResult((IReadOnlyCollection<InferenceFeedback>)new List<InferenceFeedback>());
        }

        public Task SaveAsync(InferenceFeedback feedback, CancellationToken cancellationToken = default)
        {
            var items = _feedback.GetOrAdd(feedback.InferenceId, _ => new List<InferenceFeedback>());
            lock (items)
            {
                items.Add(feedback);
            }
            return Task.CompletedTask;
        }
    }
}
