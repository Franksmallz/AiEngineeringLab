using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class StructuredFieldEvaluator : IStructuredFieldEvaluator
    {
        private readonly IEvaluationSchemaValidator _evaluationSchemaValidator;
        private readonly IExpectedAnswerParser _expectedAnswerParser;

        public StructuredFieldEvaluator(IEvaluationSchemaValidator evaluationSchemaValidator, 
            IExpectedAnswerParser expectedAnswerParser)
        {
            _evaluationSchemaValidator = evaluationSchemaValidator;
            _expectedAnswerParser = expectedAnswerParser;
        }

        public StructuredFieldScore Score(string expected, string actual)
        {
            var expectedParsed = _expectedAnswerParser.Parse(expected);
            if (!_evaluationSchemaValidator.IsCompliant(actual))
            {
                return new StructuredFieldScore
                {
                    CategoryCorrect = false,
                    RetryableCorrect = false,
                    ActionCorrect = false,
                };
            }

            var actualParsed = _expectedAnswerParser.Parse(actual);

            return new StructuredFieldScore
            {
                CategoryCorrect = string.Equals(expectedParsed.Category, actualParsed.Category, StringComparison.OrdinalIgnoreCase),
                RetryableCorrect = string.Equals(expectedParsed.Retryable, actualParsed.Retryable, StringComparison.OrdinalIgnoreCase),
                ActionCorrect = string.Equals(expectedParsed.Action, actualParsed.Action, StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
