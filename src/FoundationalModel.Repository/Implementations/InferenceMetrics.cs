using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using System.Collections.Concurrent;

namespace FoundationalModel.Repository.Implementations
{
    public class InferenceMetrics : IInferenceMetrics
    {
        private readonly ConcurrentDictionary<string, DeploymentMetrics> _metrics = new();
        public void RecordFeedback(string deploymentId, bool isCorrect)
        {
            var metrics = _metrics.GetOrAdd(deploymentId, _ => new DeploymentMetrics());

            lock (metrics)
            {
                metrics.TotalFeedback++;
                if (!isCorrect)
                {
                    metrics.NegativeFeedback++;
                }
                else
                {
                    metrics.PositiveFeedback++;
                }
            }
        }

        public void RecordInference(string deploymentId, long latencyMs, bool success, bool structuredOutputValid)
        {
            var metrics = _metrics.GetOrAdd(deploymentId, _ => new DeploymentMetrics());

            lock (metrics)
            {
                metrics.TotalInferences++;
                metrics.TotalLatencyMs += latencyMs;
                if (!success)
                {
                    metrics.Failures++;
                }
                if (!structuredOutputValid)
                {
                    metrics.InvalidStructuredOutputs++;
                }
            }
        }

        public void RecordReviewOutcome(string deploymentId, ReviewStatus status)
        {
            var metrics = _metrics.GetOrAdd(deploymentId, _ => new DeploymentMetrics());
            lock (metrics)
            {
                switch (status)
                {
                    case ReviewStatus.Approved:
                        metrics.ApprovedCorrections++;
                        break;
                    case ReviewStatus.Rejected:
                        metrics.RejectedCorrections++;
                        break;
                }
            }
        }

        public DeploymentMetrics Get(string deploymentId)
        {
            _metrics.TryGetValue(deploymentId, out var metrics);
            return metrics ?? new DeploymentMetrics();
        }
    }
}
