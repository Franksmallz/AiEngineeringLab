using Autofac;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FoundationalModel.Services.Implementations
{
    public class ImplementationResolverService : IImplementationResolverService
    {
        private readonly IComponentContext _scope;
        private readonly ILogger<ImplementationResolverService> _logger;

        public ImplementationResolverService(
            IComponentContext scope,
            ILogger<ImplementationResolverService> logger)
        {
            _scope = scope;
            _logger = logger;
        }

       

        public IModelProvider ResolveProvider(Core.Enums.Providers provider)
        {
            try
            {
                return _scope.ResolveKeyed<IModelProvider>(provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while resolving implementation.");
                throw;
            }
        }
    }
}
