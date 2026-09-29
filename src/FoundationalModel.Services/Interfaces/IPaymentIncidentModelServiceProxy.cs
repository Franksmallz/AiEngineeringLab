using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IPaymentIncidentModelServiceProxy : IAutoDependencyService
    {
        Task<PaymentIncidentResponse> GenerateAsync(PaymentIncidentRequest request);
    }
}
