using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Services.Interfaces
{
    public interface IFeedbackService : IAutoDependencyService
    {
       public Task<string>SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default);
    }
}
