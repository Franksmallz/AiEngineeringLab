namespace FoundationalModel.Models.Dtos.Requests
{
    public class DatasetScenario
    {
        public string Category { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        public string Retryable { get; set; } = string.Empty;
        public string Action {  get; set; } = string.Empty;

        public static IReadOnlyList<DatasetScenario> Scenarios = new List<DatasetScenario>
        {
            new()
            {
                Category = "Provider Timeout",
                Input = "The transfer request timed out before the provider acknowledged receiving it.",
                Retryable = "Yes",
                Action = "Retry the request."
            },

            new()
            {
                Category = "Provider Timeout",
                Input = "The provider acknowledged the transfer request but did not return a final transaction status before the timeout.",
                Retryable = "Yes",
                Action = "Query provider before retrying."
            },

            new()
            {
                Category = "Provider Timeout",
                Input = "The payment request timed out after the provider began processing it, and it is unclear whether the customer was debited.",
                Retryable = "Yes",
                Action = "Query transaction status before retrying."
            },

            new()
            {
                Category = "Provider Timeout",
                Input = "Multiple payment requests are timing out while the provider is experiencing degraded response times.",
                Retryable = "Yes",
                Action = "Retry with backoff."
            },

            new()
            {
                Category = "Provider Timeout",
                Input = "The same payment request has timed out repeatedly despite several automatic retry attempts.",
                Retryable = "No",
                Action = "Stop automatic retries and escalate."
            },
            new DatasetScenario
            {
                Category = "Insufficient Funds",
                Input = "The customer attempted a transfer, but the available account balance was lower than the transfer amount.",
                Retryable = "No",
                Action = "Ask the customer to fund the account before retrying."
            },

            new DatasetScenario
            {
                Category = "Insufficient Funds",
                Input = "The payment failed because the customer's balance could cover the transaction amount but not the additional transaction fee.",
                Retryable = "No",
                Action = "Ask the customer to add enough funds to cover the amount and fees."
            },

            new DatasetScenario
            {
                Category = "Insufficient Funds",
                Input = "A scheduled payment was attempted overnight after another debit reduced the customer's available balance.",
                Retryable = "Yes",
                Action = "Retry after sufficient funds become available."
            },

            new DatasetScenario
            {
                Category = "Insufficient Funds",
                Input = "A recurring payment failed because the customer's account did not have enough available funds at the scheduled execution time.",
                Retryable = "Yes",
                Action = "Retry according to the recurring-payment retry policy."
            },

            new DatasetScenario
            {
                Category = "Insufficient Funds",
                Input = "The same transfer has failed repeatedly because the account balance has remained below the required amount.",
                Retryable = "No",
                Action = "Stop automatic retries until the account is funded."
            },
            new DatasetScenario
            {
                Category = "Webhook Delivery Failure",
                Input = "The payment completed successfully, but the merchant webhook endpoint returned HTTP 500.",
                Retryable = "Yes",
                Action = "Retry webhook delivery using backoff."
            },

            new DatasetScenario
            {
                Category = "Webhook Delivery Failure",
                Input = "The merchant webhook endpoint could not be reached because the connection timed out.",
                Retryable = "Yes",
                Action = "Retry webhook delivery after a delay."
            },

            new DatasetScenario
            {
                Category = "Webhook Delivery Failure",
                Input = "Webhook delivery failed because the merchant endpoint returned HTTP 401 after its authentication credentials expired.",
                Retryable = "No",
                Action = "Stop retries and request updated webhook credentials."
            },

            new DatasetScenario
            {
                Category = "Webhook Delivery Failure",
                Input = "The merchant endpoint rejected the webhook payload with HTTP 400 because the payload format was invalid.",
                Retryable = "No",
                Action = "Fix the webhook payload before attempting delivery again."
            },

            new DatasetScenario
            {
                Category = "Webhook Delivery Failure",
                Input = "Webhook delivery has failed repeatedly and has already reached the maximum configured retry attempts.",
                Retryable = "No",
                Action = "Move the webhook to failed delivery handling and escalate."
            },

            new DatasetScenario
            {
                Category = "Invalid Beneficiary Details",
                Input = "The transfer failed because the beneficiary account number does not exist at the destination bank.",
                Retryable = "No",
                Action = "Ask the customer to verify the beneficiary account details before retrying."
            },

            new DatasetScenario
            {
                Category = "Invalid Beneficiary Details",
                Input = "The beneficiary bank code supplied with the transfer does not match a supported destination bank.",
                Retryable = "No",
                Action = "Correct the beneficiary bank information before retrying."
            },

            new DatasetScenario
            {
                Category = "Invalid Beneficiary Details",
                Input = "The beneficiary account number is valid, but the account name returned by name enquiry does not match the intended recipient.",
                Retryable = "No",
                Action = "Require the customer to confirm the beneficiary before proceeding."
            },

            new DatasetScenario
            {
                Category = "Invalid Beneficiary Details",
                Input = "The transfer was rejected because the beneficiary account has been closed by the destination bank.",
                Retryable = "No",
                Action = "Request a different beneficiary account."
            },

            new DatasetScenario
            {
                Category = "Invalid Beneficiary Details",
                Input = "The beneficiary information is incomplete because a required routing field was omitted from the transfer request.",
                Retryable = "No",
                Action = "Provide the missing beneficiary information before retrying."
            },

            new DatasetScenario
            {
                Category = "Provider Unavailable",
                Input = "The payment provider returned a service unavailable response while attempting to process the transfer.",
                Retryable = "Yes",
                Action = "Retry the request using backoff."
            },

            new DatasetScenario
            {
                Category = "Provider Unavailable",
                Input = "The provider endpoint could not be reached because the service was temporarily offline.",
                Retryable = "Yes",
                Action = "Retry after a short delay."
            },

            new DatasetScenario
            {
                Category = "Provider Unavailable",
                Input = "The provider is currently in a scheduled maintenance window and is not accepting payment requests.",
                Retryable = "Yes",
                Action = "Wait until the maintenance window ends before retrying."
            },

            new DatasetScenario
            {
                Category = "Provider Unavailable",
                Input = "The provider has remained unavailable across several retry attempts and the outage is still ongoing.",
                Retryable = "No",
                Action = "Stop automatic retries and escalate the provider outage."
            },

            new DatasetScenario
            {
                Category = "Provider Unavailable",
                Input = "The primary payment provider is unavailable, but a configured backup provider is available for the transaction.",
                Retryable = "Yes",
                Action = "Route the transaction through the backup provider."
            },

            new DatasetScenario
            {
                Category = "Expired Card",
                Input = "The card payment was declined because the card expiry date has already passed.",
                Retryable = "No",
                Action = "Ask the customer to use a valid card."
            },

            new DatasetScenario
            {
                Category = "Expired Card",
                Input = "A recurring card payment failed because the stored card expired before the scheduled charge date.",
                Retryable = "No",
                Action = "Request updated card details before the next payment attempt."
            },

            new DatasetScenario
            {
                Category = "Expired Card",
                Input = "The customer attempted to pay with a saved card whose expiry date is no longer valid.",
                Retryable = "No",
                Action = "Remove or replace the expired saved card."
            },

            new DatasetScenario
            {
                Category = "Expired Card",
                Input = "The subscription renewal failed because the card on file expired this month and the issuer rejected the transaction.",
                Retryable = "No",
                Action = "Ask the customer to update the payment method before retrying the renewal."
            },

            new DatasetScenario
            {
                Category = "Expired Card",
                Input = "Several payment attempts were made using the same expired card details.",
                Retryable = "No",
                Action = "Stop retries until valid card details are provided."
            },
            new DatasetScenario
            {
                Category = "Duplicate Request",
                Input = "The same transfer request was submitted twice with the same idempotency key before the first request completed.",
                Retryable = "No",
                Action = "Return the result of the original request instead of creating another transaction."
            },

            new DatasetScenario
            {
                Category = "Duplicate Request",
                Input = "A client retried a payment request after a network error using the same transaction reference, but the original payment had already completed successfully.",
                Retryable = "No",
                Action = "Return the completed transaction result and do not process another payment."
            },

            new DatasetScenario
            {
                Category = "Duplicate Request",
                Input = "Two identical transfer requests arrived within milliseconds using different client request identifiers.",
                Retryable = "No",
                Action = "Block the duplicate request and investigate the missing idempotency protection."
            },

            new DatasetScenario
            {
                Category = "Duplicate Request",
                Input = "The customer submitted the same payment again after seeing a pending status, while the original transaction is still being processed.",
                Retryable = "No",
                Action = "Query the original transaction status and do not create a second payment."
            },

            new DatasetScenario
            {
                Category = "Duplicate Request",
                Input = "A duplicate payment request was detected after the original transaction had already been reversed.",
                Retryable = "Yes",
                Action = "Process a new request only after confirming the reversal and using a new transaction reference."
            },
            new DatasetScenario
            {
                Category = "Pending Provider Confirmation",
                Input = "The provider accepted the transfer request, but the transaction is still marked as pending while final confirmation is being processed.",
                Retryable = "No",
                Action = "Wait for the provider's final status before taking further action."
            },

            new DatasetScenario
            {
                Category = "Pending Provider Confirmation",
                Input = "The transaction has remained pending beyond the normal processing time, but the provider has not returned a final success or failure status.",
                Retryable = "No",
                Action = "Query the provider for the latest transaction status."
            },

            new DatasetScenario
            {
                Category = "Pending Provider Confirmation",
                Input = "A customer attempted the same payment again because the first transaction was still pending.",
                Retryable = "No",
                Action = "Do not create a second payment; check the original transaction status."
            },

            new DatasetScenario
            {
                Category = "Pending Provider Confirmation",
                Input = "The provider returned a pending response and later status polling still shows the transaction as processing.",
                Retryable = "No",
                Action = "Continue status polling according to the configured pending-transaction policy."
            },

            new DatasetScenario
            {
                Category = "Pending Provider Confirmation",
                Input = "A transaction has remained pending for an unusually long period and has exceeded the configured pending-status threshold.",
                Retryable = "No",
                Action = "Escalate the transaction for reconciliation and manual review."
            },

            new DatasetScenario
            {
                Category = "Debit Without Final Status",
                Input = "The customer's account was debited, but the transfer did not receive a final success or failure status from the provider.",
                Retryable = "No",
                Action = "Query the provider and reconcile the transaction before any retry."
            },

            new DatasetScenario
            {
                Category = "Debit Without Final Status",
                Input = "The customer's balance reduced after the payment attempt, but the provider response was lost before a final transaction status was recorded.",
                Retryable = "No",
                Action = "Check the provider transaction status before taking further action."
            },

            new DatasetScenario
            {
                Category = "Debit Without Final Status",
                Input = "A transfer has remained unresolved after the customer was debited, and repeated status checks still return no final result.",
                Retryable = "No",
                Action = "Escalate the transaction for reconciliation and manual investigation."
            },

            new DatasetScenario
            {
                Category = "Debit Without Final Status",
                Input = "The customer was debited for a payment, but the beneficiary has not received value and the provider has not confirmed the outcome.",
                Retryable = "No",
                Action = "Reconcile the transaction and confirm the provider status before deciding on reversal or completion."
            },

            new DatasetScenario
            {
                Category = "Debit Without Final Status",
                Input = "The customer was debited, but the provider later confirmed that the transaction failed and no value was delivered.",
                Retryable = "Yes",
                Action = "Confirm or complete the reversal before allowing a new payment attempt."
            },

            new DatasetScenario
            {
                Category = "Provider Authentication Failure",
                Input = "The provider rejected the payment request because the configured API credential had expired.",
                Retryable = "No",
                Action = "Refresh or replace the provider credential before retrying."
            },

            new DatasetScenario
            {
                Category = "Provider Authentication Failure",
                Input = "The payment request was rejected because the API key configured for the provider had been revoked.",
                Retryable = "No",
                Action = "Replace the revoked API key before retrying."
            },

            new DatasetScenario
            {
                Category = "Provider Authentication Failure",
                Input = "The provider returned an authentication error because the request signature could not be validated.",
                Retryable = "No",
                Action = "Verify the signing configuration and regenerate the request signature."
            },

            new DatasetScenario
            {
                Category = "Provider Authentication Failure",
                Input = "The provider rejected the request because the client identifier and secret did not match the configured merchant account.",
                Retryable = "No",
                Action = "Correct the provider authentication configuration before retrying."
            },

            new DatasetScenario
            {
                Category = "Provider Authentication Failure",
                Input = "Payment attempts continue to fail authentication after several retries using the same credentials.",
                Retryable = "No",
                Action = "Stop retries and escalate the provider authentication configuration issue."
            }
        };
    }
}
