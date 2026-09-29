using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using System.Collections.Concurrent;

namespace FoundationalModel.Repository.Implementations
{
    public class InferenceRecordRepository : IInferenceRecordRepository
    {
        private readonly ConcurrentDictionary<string, InferenceRecord> _records = new();
        
        public Task SaveAsync(InferenceRecord record, CancellationToken cancellationToken = default)
        {
            _records[record.InferenceId] = record;
            return Task.CompletedTask;
        }

        public Task<InferenceRecord?> GetAsync(string inferenceId, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(inferenceId, out var record);

             return Task.FromResult(record);
        }
    }
}
