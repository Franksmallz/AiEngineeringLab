using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class DatasetValidator : IDatasetValidator
    {
        public List<ValidationIssue> Validate(IReadOnlyList<ParsedTrainingExample> examples)
        {
            var issues = new List<ValidationIssue>();

            for(var i = 0; i < examples.Count; i++)
            {
                var example = examples[i];

                if (string.IsNullOrWhiteSpace(example.Input))
                {
                    issues.Add(new ValidationIssue
                    {
                        Index = i,
                        Message = "Input is missing."

                    });
                }

                if (string.IsNullOrWhiteSpace(example.Category))
                {
                    issues.Add(new ValidationIssue
                    {
                        Index = i,
                        Message = "Category is missing."

                    });
                }

                if (string.IsNullOrWhiteSpace(example.Retryable))
                {
                    issues.Add(new ValidationIssue
                    {
                        Index = i,
                        Message = "Retryable is missing."

                    });
                }

                if(!string.Equals(example.Retryable, "Yes", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(example.Retryable, "No", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue
                    {
                        Index = i,
                        Message = $"Invalid Retryable value: {example.Retryable}"
                    });
                }

                if (string.IsNullOrWhiteSpace(example.Action))
                {
                    issues.Add(new ValidationIssue
                    {
                        Index = i,
                        Message = "Action is missing."

                    });
                }
            }

            return issues;
        }
    }
}
