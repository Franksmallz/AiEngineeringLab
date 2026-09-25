namespace FoundationalModel.Services.Interfaces
{
    public interface IInferenceBenchmarkWriter : IAutoDependencyService
    {
        public Task Write(string json, string directory, string filename);
    }
}
