using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IPaymentIncidentInferenceService : IAutoDependencyService
    {
        Task<PaymentIncidentResponse> Generate(PaymentIncidentRequest request);
    }
}
