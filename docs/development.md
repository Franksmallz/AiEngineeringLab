# Development guide

## Design defaults

- Target .NET 8 and use asynchronous I/O for external work.
- Keep HTTP contracts in `Contracts/` and business rules out of controllers.
- Prefer explicit dependencies through dependency injection.
- Use UTC for timestamps crossing process boundaries.
- Return useful problem details for unexpected failures without exposing secrets.
- Add tests for behaviour, not implementation details.

## AI change checklist

Before merging an AI-assisted change, record:

- the problem and intended behaviour;
- which files the tool changed;
- assumptions the tool made;
- tests run and their results;
- security, privacy, and dependency checks;
- what a human reviewer changed or rejected.
