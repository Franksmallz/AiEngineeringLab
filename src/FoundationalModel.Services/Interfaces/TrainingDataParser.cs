using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public class TrainingDataParser : ITrainingDataParser
    {
        public ParsedTrainingExample Parse(TrainingExample trainingExample)
        {
            var lines = trainingExample.Output
                .Split('\n')
                .Select(x => x.Trim())
                .ToArray();

            var parsed = new ParsedTrainingExample
            {
                Input = trainingExample.Input,
            };

            foreach(var line in lines)
            {
                if(line.StartsWith("Category:", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Category = line["Category:".Length..].Trim();
                }
                else if (line.StartsWith("Retryable:", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Retryable = line["Retryable:".Length..].Trim();
                }
                else if (line.StartsWith("Action:", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Action = line["Action:".Length..].Trim();
                }
            }

            return parsed;
        }
    }
}
