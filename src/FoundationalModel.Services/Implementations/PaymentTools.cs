using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class PaymentTools : IPaymentTools
    {
        public Task<decimal> GetCustomerBalance(string customerId)
        {
            throw new NotImplementedException();
        }

        public Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId)
        {
            return Task.FromResult(new PaymentStatusResult
            {
                PaymentId = paymentId,
                Status = "Pending",
                Reason = "Provider timeout",
                TenantId = "test_tenant"
            });
        }
    }
}
