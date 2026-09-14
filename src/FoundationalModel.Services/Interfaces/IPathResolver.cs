namespace FoundationalModel.Services.Interfaces
{
    public interface IPathResolver : IAutoDependencyService
    {
        public string ResolveConfiguredPath(string configuredPath);
    }
}
