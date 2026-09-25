using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class InferenceBenchmarkWriter : IInferenceBenchmarkWriter
    {
        private readonly IPathResolver _pathResolver;

        public InferenceBenchmarkWriter(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public async Task Write(string json, string directory, string fileName)
        {
            var path = _pathResolver.ResolveConfiguredPath(directory);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            File.WriteAllTextAsync(Path.Combine(path, fileName), json);
        }
    }
}
