using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.ML;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Interfaces;
using RiskClassificationLab.Services.Interfaces.ML;

namespace RiskClassificationLab.Services.Implementations.ML
{
    public class FinalModelStore : IFinalModelStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        private readonly IPathResolver _pathResolver;

        public FinalModelStore(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public string SaveModel(MLContext mlContext, ITransformer model, DataViewSchema inputSchema, string configuredPath)
        {
            var path = _pathResolver.ResolveConfiguredPath(configuredPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            mlContext.Model.Save(model, inputSchema, path);
            return ContentSha256(path);
        }

        public ITransformer LoadModel(MLContext mlContext, string configuredPath, string expectedContentSha256)
        {
            var path = _pathResolver.ResolveConfiguredPath(configuredPath);
            var actual = ContentSha256(path);
            if (actual != expectedContentSha256)
                throw new InvalidOperationException(
                    $"{configuredPath}: content SHA-256 {actual} does not match the frozen {expectedContentSha256}.");

            // The engineered-feature mapping is custom code; ML.NET finds it
            // by its contract name in this assembly.
            mlContext.ComponentCatalog.RegisterAssembly(typeof(EngineeredFeaturesFactory).Assembly);
            return mlContext.Model.Load(path, out _);
        }

        public bool Exists(string configuredPath) =>
            File.Exists(_pathResolver.ResolveConfiguredPath(configuredPath));

        public void WriteJson<T>(string configuredPath, T value, bool overwrite)
        {
            var path = _pathResolver.ResolveConfiguredPath(configuredPath);
            if (!overwrite && File.Exists(path))
                throw new InvalidOperationException($"{configuredPath} already exists and must not be overwritten.");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        }

        public T ReadJson<T>(string configuredPath) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(_pathResolver.ResolveConfiguredPath(configuredPath)), JsonOptions)
            ?? throw new InvalidOperationException($"{configuredPath} is empty.");

        // A zip file stores the time each entry was written, so the file hash
        // changes on every save. This hash covers only the entry names and
        // their bytes, so the same model always gives the same hash.
        private static string ContentSha256(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            using var sha = SHA256.Create();

            foreach (var entry in archive.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                var name = Encoding.UTF8.GetBytes(entry.FullName);
                sha.TransformBlock(name, 0, name.Length, null, 0);

                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var bytes = buffer.ToArray();
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }

            sha.TransformFinalBlock([], 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }
    }
}
