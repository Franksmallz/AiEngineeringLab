using Microsoft.ML;

namespace RiskClassificationLab.Services.Interfaces.ML
{
    // Saves and loads the frozen Chapter 6 model and its JSON records.
    public interface IFinalModelStore : IAutoDependencyService
    {
        // Saves the model and returns the SHA-256 of its content.
        string SaveModel(MLContext mlContext, ITransformer model, DataViewSchema inputSchema, string configuredPath);

        // Loads the model; fails if its content hash is not the expected one.
        ITransformer LoadModel(MLContext mlContext, string configuredPath, string expectedContentSha256);

        bool Exists(string configuredPath);
        void WriteJson<T>(string configuredPath, T value, bool overwrite);
        T ReadJson<T>(string configuredPath);
    }
}
