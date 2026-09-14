using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations
{
    public class ExecuteAgenticToolService : IExecuteAgenticToolService
    {
        private readonly IPaymentTools _paymentTools;
        private readonly IKnowledgeSearchTool _knowledgeSearchTool;

        public ExecuteAgenticToolService(IPaymentTools paymentTools, IKnowledgeSearchTool knowledgeSearchTool)
        {
            _paymentTools = paymentTools;
            _knowledgeSearchTool = knowledgeSearchTool;
        }

        public async Task<ToolExecutionResult> ExecuteToolAsync(string toolName, 
            IReadOnlyDictionary<string, JsonElement> inputs, UserContext user)
        {
            try
            {
                return toolName switch
                {
                    "get_payment_status" =>
                    await ExecutePaymentStatusAsync(inputs, user),

                    "get_customer_balance" =>
                    await ExecuteGetCustomerBalanceAsync(inputs, user),

                    "search_knowledge_base" =>
                    await ExccuteKnowledgeBaseSearchAsync(inputs, user),

                    _ => new ToolExecutionResult
                    {
                        Success = false,
                        ErrorCode = "UNKNOWN_TOOL",
                        ErrorMessage = $"Tool '{toolName}' is not available"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "TOOL_EXECUTION_FAILED",
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<ToolExecutionResult> ExecutePaymentStatusAsync(IReadOnlyDictionary<string, JsonElement> inputs, UserContext user)
        {
            if (!user.Permissions.Contains("payment.read"))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "FORBIDDEN",
                    ErrorMessage = "User is not allowed to access the payment tool"
                };
            }
            if (!inputs.TryGetValue("paymentId", out var paymenIdElement) || string.IsNullOrWhiteSpace(paymenIdElement.GetString()))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "MISSING_ARGUMENT",
                    ErrorMessage = "PaymentId is required"
                };
            }

            var paymentId = paymenIdElement.GetString();

            if(paymentId.Length > 35)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "INVALID_ARGUMENT",
                    ErrorMessage = "PaymentId is too long"
                };
            }

            var payment = await _paymentTools.GetPaymentStatusAsync(paymentId);
            if(payment == null)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "PAYMENT_NOT_FOUND",
                    ErrorMessage = $"No payment was found for '{paymentId}'."
                };
            }

            if(payment.TenantId != user.TenantId)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "FORBIDDEN",
                    ErrorMessage = "User can not access this payment"
                };
            }

            return new ToolExecutionResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(payment)
            };
        }

        private async Task<ToolExecutionResult> ExecuteGetCustomerBalanceAsync(IReadOnlyDictionary<string, JsonElement> inputs, UserContext user)
        {
            if (!user.Permissions.Contains("balance.read"))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "FORBIDDEN",
                    ErrorMessage = "User is not allowed to access the balance tool"
                };
            }
            if (!inputs.TryGetValue("customerId", out var customerIdElement) || string.IsNullOrWhiteSpace(customerIdElement.GetString()))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "MISSING_ARGUMENT",
                    ErrorMessage = "customerId is required"
                };
            }

            var customerId = customerIdElement.GetString();

            var customerBalance = await _paymentTools.GetCustomerBalance(customerId);

            return new ToolExecutionResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(customerBalance)
            };
        }

        private async Task<ToolExecutionResult> ExccuteKnowledgeBaseSearchAsync(IReadOnlyDictionary<string, JsonElement> inputs, UserContext user)
        {
            if (!user.Permissions.Contains("knowledge_read"))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "FORBIDDEN",
                    ErrorMessage = "User is not allowed to access the knowledge base tool"
                };
            }
            if (!inputs.TryGetValue("query", out var queryElement) || string.IsNullOrWhiteSpace(queryElement.GetString()))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "MISSING_ARGUMENT",
                    ErrorMessage = "query is required"
                };
            }

            var query = queryElement.GetString();

            var retrievedDocs = await _knowledgeSearchTool.SearchAsync(query, CancellationToken.None);

            if(retrievedDocs.Count == 0)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    ErrorCode = "NO_KNOWLEDGE_FOUND",
                    ErrorMessage = "No relevant documentation was found"
                };
            }

            return new ToolExecutionResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(retrievedDocs)
            };
        }
    }
}
