# Webhooks

Webhooks are HTTP callbacks that allow one system to notify another system when an event occurs. Instead of repeatedly polling an API, the receiving system exposes an endpoint and the sending system delivers event messages to it.

## Why Webhooks Matter

Webhooks are useful when a result is produced asynchronously or may change after the original request has completed. Common examples include payment updates, order status changes, account events, and background job completion.

- **Near real-time updates:** Consumers receive changes shortly after they happen.
- **Lower polling overhead:** Consumers do not need to repeatedly ask whether anything changed.
- **Asynchronous workflows:** A request can be accepted first and completed later.
- **Loose coupling:** The producer and consumer can evolve independently around a documented event contract.

## Typical Webhook Flow

1. The consumer registers or configures a callback URL.
2. The producer records an event.
3. The producer sends an HTTP request to the callback URL.
4. The consumer authenticates and validates the request.
5. The consumer processes the event and returns a successful status code.
6. The producer records the delivery result and retries when appropriate.

The consumer should acknowledge quickly. Long-running work should be placed on a queue after the event has been validated.

## Example Event

```json
{
  "id": "evt_01JABC123",
  "type": "payment.succeeded",
  "occurredAt": "2026-09-11T10:30:00Z",
  "version": 1,
  "data": {
    "paymentId": "pay_123",
    "amount": 10000,
    "currency": "NGN",
    "status": "succeeded"
  }
}
```

Every event should have a stable event ID, event type, creation timestamp, schema version, and business payload. The event ID is important for deduplication.

## Reliability Requirements

Webhook delivery is normally at-least-once. A consumer may receive the same event more than once, so processing must be idempotent.

- Store the event ID before applying a non-repeatable side effect, or use a database unique constraint.
- Return a success status only after the event has been durably accepted.
- Treat timeouts and most `5xx` responses as retryable.
- Use exponential backoff with a maximum retry count.
- Send permanently failed events to a dead-letter queue or delivery dashboard.
- Preserve the original event ID across all retries.

## Security

- Use HTTPS for every production callback URL.
- Authenticate requests with a signature, shared secret, mTLS, or an equivalent mechanism.
- Include a timestamp in the signed material and reject messages outside an allowed clock-skew window.
- Protect against replay by recording event IDs and enforcing signature expiry.
- Validate the payload schema, event type, and expected account or tenant.
- Do not trust identifiers or amounts solely because they arrived in a webhook; validate them against the system of record when necessary.

A common signature pattern is:

```text
signature = HMAC-SHA256(secret, timestamp + "." + raw_request_body)
```

The signature must be calculated over the raw request body before JSON parsing so that whitespace or field-order changes do not invalidate verification unexpectedly.

## Versioning and Compatibility

Prefer additive changes to an event schema. Add optional fields instead of renaming or removing fields that existing consumers may depend on. Include a version when a breaking change is unavoidable, and support old versions during a defined migration period.

## Testing Checklist

- Valid signatures are accepted.
- Invalid, expired, and replayed signatures are rejected.
- Duplicate event delivery does not duplicate side effects.
- Unknown event types are handled safely.
- Malformed payloads return a clear client error.
- Temporary downstream failures are retried.
- Permanent failures are visible through logs and dead-letter handling.
- Events are processed correctly when delivered out of order.
- Sensitive data is not written to logs.
