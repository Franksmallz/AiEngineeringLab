using FoundationalModel.Core.Enums;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IImplementationResolverService _implementationResolverService;

        public EmbeddingService(IImplementationResolverService implementationResolverService)
        {
            _implementationResolverService = implementationResolverService;
        }

        public async Task<float[]> CreateEmbeddingAsync(string text)
        {
            var providerImplementation = _implementationResolverService.ResolveProvider(EmbeddingProvider.OPENAI);
            return await providerImplementation.CreateEmbeddingAsync(text);
        }
    }
}
