using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Entities;

namespace FoundationalModel.Repository.Interfaces
{
    public interface IInferenceMetrics : IAutoDependencyRepository
    {
        void RecordInference(string deploymentId, long latencyMs, bool success, bool structuredOutputValid);
        void RecordFeedback(string deploymentId, bool isCorrect);
        void RecordReviewOutcome(string deploymentId, ReviewStatus status);
        public DeploymentMetrics? Get(string deploymentId);

    }
}
