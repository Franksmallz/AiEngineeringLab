using FoundationalModel.Models.Entities;
using FoundationalModel.Repository.Interfaces;
using System.Collections.Concurrent;

namespace FoundationalModel.Repository.Implementations
{
    public class ModelDeploymentRepository : IModelDeploymentRepository
    {
        private readonly ConcurrentDictionary<string, ModelDeployment> _deployments = new();
        public Task<ModelDeployment?> GetAsync(string deploymentId, CancellationToken cancellationToken = default)
        {
            _deployments.TryGetValue(deploymentId, out var deployment);
            return Task.FromResult(deployment);
        }

        public Task SaveAsync(ModelDeployment deployment, CancellationToken cancellationToken = default)
        {
            _deployments[deployment.DeploymentId] = deployment;
            return Task.CompletedTask;
        }
    }
}
