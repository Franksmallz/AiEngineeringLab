using Autofac;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Services.Implementations
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

        public IRiskClassifier ResolveClassifier(string ruleProvider)
        {
            try
            {
                return _scope.ResolveKeyed<IRiskClassifier>(ruleProvider.ToUpper());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while resolving implementation.");
                throw;
            }
        }
    }
}
