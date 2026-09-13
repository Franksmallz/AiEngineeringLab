using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IPaymentTools : IAutoDependencyService
    {
        Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId);
        Task<decimal> GetCustomerBalance(string customerId);
    }
}
