using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IDataLoader : IAutoDependencyService
    {
        public List<TrainingExample> Load(string filename);
    }
}
