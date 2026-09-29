using FoundationalModel.Models.Entities;

namespace FoundationalModel.Repository.Interfaces
{
    public interface IModelDeploymentRepository : IAutoDependencyRepository
    {
        Task SaveAsync(ModelDeployment deployment, CancellationToken cancellationToken = default);
        Task<ModelDeployment?> GetAsync(string deploymentId, CancellationToken cancellationToken = default);
    }
}
