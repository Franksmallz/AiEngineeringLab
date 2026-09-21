using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class ExpectedAnswerParser : IExpectedAnswerParser
    {
        public ParsedExpectedAnswer  Parse(string expected)
        {
            var result = new ParsedExpectedAnswer();

            var lines = expected
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            foreach (var line in lines)
            {
                if (line.StartsWith(
                    "Category:",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result.Category =
                        line["Category:".Length..].Trim();
                }
                else if (line.StartsWith(
                    "Retryable:",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result.Retryable =
                        line["Retryable:".Length..].Trim();
                }
                else if (line.StartsWith(
                    "Action:",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result.Action =
                        line["Action:".Length..].Trim();
                }
            }

            return result;
        }
    }
}
