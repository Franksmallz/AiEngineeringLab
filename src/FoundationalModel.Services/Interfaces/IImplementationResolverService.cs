using FoundationalModel.Core.Enums;

namespace FoundationalModel.Services.Interfaces
{
    public interface IImplementationResolverService : IAutoDependencyService
    {
        IModelProvider ResolveProvider(Providers provider);
    }
}
