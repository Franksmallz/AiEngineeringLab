using FoundationalModel.Core.Enums;

namespace FoundationalModel.Services.Interfaces
{
    public interface IImplementationResolverService : IAutoDependencyService
    {
        IModelProvider ResolveProvider(Providers provider);
        IEmbeddingProvider ResolveProvider(Core.Enums.EmbeddingProvider provider);
    }
}
