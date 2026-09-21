using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class EvaluatorSchemaValidator : IEvaluationSchemaValidator
    {
        public bool IsCompliant(string actual)
        {
            return actual.Contains(
                    "Category:",
                    StringComparison.OrdinalIgnoreCase)
             && actual.Contains(
                    "Retryable:",
                    StringComparison.OrdinalIgnoreCase)
             && actual.Contains(
                    "Action:",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
