# Understanding Idempotency

Idempotency is a foundational concept in software engineering, API design, and distributed systems.

## What Is Idempotency?

Idempotency is the property of an operation where executing it multiple times has the exact same side effect as executing it a single time. In simple terms, once a request succeeds, repeating it should not change the state of the system further or create unintended duplicate records.

## Why It Matters

In distributed systems, networks are inherently unreliable. Requests can time out, drop, or fail halfway through. Idempotency guarantees safety by allowing clients to retry failed or ambiguous operations without corrupting data.

- **Prevents duplication:** Avoids creating duplicate orders, charging a customer's credit card twice, or adding multiple identical rows to a database.
- **Enables safe retries:** If a timeout occurs, the client can safely resend the exact same request until a confirmation is received.
- **Improves system resilience:** Network glitches or crashed microservices can be recovered automatically by replaying events without manual database cleanup.

## Real-World Examples

### Idempotent Operations (Safe to Repeat)

- **Setting a value:** Changing a status to `PAID` or `ACTIVE`. No matter how many times the operation runs, the status remains unchanged.
- **Deletion:** Deleting a record with a specific ID. The first call removes it; subsequent calls find nothing to delete but leave the system in the same final state.
- **Turning something on:** Turning a light switch `ON`. If it is already on, repeating the operation changes nothing.

### Non-Idempotent Operations (Dangerous to Repeat)

- **Increments:** Adding $10 to an account balance. Every retry adds another $10.
- **Blind creations:** Creating a new user or order without an explicit client-side identifier. Each retry creates a new duplicate row.
- **Toggling:** Pressing a play/pause button. Repeating the action flips the state back and forth.

## Common Implementation Strategies

### Idempotency Keys

The client attaches a unique identifier, such as a UUID, to the request header. The server tracks this key in a fast cache such as Redis. If the same key arrives again, the server skips processing and replays the cached original response.

### Unique Constraints

Strict database-level unique keys, such as email addresses or composite tokens, allow the database itself to block duplicate entry attempts.
