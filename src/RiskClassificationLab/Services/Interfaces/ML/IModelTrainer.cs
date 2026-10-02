namespace RiskClassificationLab.Services.Interfaces.ML
{
    public interface IModelTrainer : IAutoDependencyService
    {
        void Train(string filename, string modelPath);
    }
}
