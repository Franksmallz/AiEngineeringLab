using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces;

public interface IRagEvaluationService : IAutoDependencyService
{
    Task<RagCombinedResult> EvaluateAsync(
        RagEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
