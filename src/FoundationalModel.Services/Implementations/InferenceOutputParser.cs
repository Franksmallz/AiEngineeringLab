using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class InferenceOutputParser : IInferenceOutputParser
    {
        public ParsedInferenceOutput Parse(string rawOutput)
        {
            if(string.IsNullOrWhiteSpace(rawOutput))
            {
                return new();
            }

            string? category = null;
            bool? retryable = null;
            string? action = null;

            var lines = rawOutput.Split('\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach(var line in lines)
            {
                if(line.StartsWith("Category:", StringComparison.OrdinalIgnoreCase))
                {
                    category = line.Substring("Category:".Length).Trim();
                }
                if (line.StartsWith("Retryable:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring("Retryable:".Length).Trim();
                    if(value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        retryable = true;
                    }
                    else if(value.Equals("No", StringComparison.OrdinalIgnoreCase))
                    {
                        retryable = false;
                    }
                }
                if (line.StartsWith("Action:", StringComparison.OrdinalIgnoreCase))
                {
                    action = line.Substring("Action:".Length).Trim();
                }
            }
            return new ParsedInferenceOutput
            {
                Category = category,
                Retryable = retryable,
                Action = action
            };
        }
    }
}
