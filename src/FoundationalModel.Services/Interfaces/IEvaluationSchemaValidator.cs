namespace FoundationalModel.Services.Interfaces
{
    public interface IEvaluationSchemaValidator : IAutoDependencyService
    {
        bool IsCompliant(string actual);
    }
}
