using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IExpectedAnswerParser : IAutoDependencyService
    {
        ParsedExpectedAnswer Parse(string expected);
    }
}
