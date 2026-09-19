using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface ITrainingDataParser : IAutoDependencyService
    {
        public ParsedTrainingExample Parse(TrainingExample trainingExample);
    }
}
