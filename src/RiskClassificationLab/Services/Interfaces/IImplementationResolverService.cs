namespace RiskClassificationLab.Services.Interfaces
{
    public interface IImplementationResolverService : IAutoDependencyService
    {
        IRiskClassifier ResolveClassifier(string ruleProvider);
    }
}
