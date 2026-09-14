using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Hosting;

namespace FoundationalModel.Services.Implementations
{
    public class PathResolver : IPathResolver
    {
        private readonly IHostEnvironment _environment;

        public PathResolver(IHostEnvironment environment)
        {
            _environment = environment;
        }

        public string ResolveConfiguredPath(string configuredPath)
        {

            if (Path.IsPathRooted(configuredPath)) return configuredPath;
            var directory = new DirectoryInfo(_environment.ContentRootPath);
            while (directory is not null)
            {
                if (directory.GetFiles("*.slnx").Length > 0 || directory.GetFiles("*.sln").Length > 0)
                    return Path.Combine(directory.FullName, configuredPath);
                directory = directory.Parent;
            }
            return Path.Combine(_environment.ContentRootPath, configuredPath);
        }
    }
}
