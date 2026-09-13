using FoundationalModel.Services.Interfaces;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations
{
    public class ExecuteAgenticToolService : IExecuteAgenticToolService
    {
        private readonly IPaymentTools _paymentTools;

        public ExecuteAgenticToolService(IPaymentTools paymentTools)
        {
            _paymentTools = paymentTools;
        }

        public async Task<object> ExecuteToolAsync(string toolName, IReadOnlyDictionary<string, JsonElement> inputs)
        {
            return toolName switch
            {
                "get_payment_status" =>
                await _paymentTools.GetPaymentStatusAsync(inputs["paymentId"].GetString()!),

                "get_customer_balance" =>
                await _paymentTools.GetCustomerBalance(inputs["customerId"].GetString()!),

                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };
        }
    }
}
