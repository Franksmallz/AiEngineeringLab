# AI Engineering Hands-On

A small, production-minded ASP.NET Core API used to practise AI-assisted software engineering.

The repository is deliberately simple. Each exercise should leave behind working code, tests, an experiment record, and an evaluation. AI may help with exploration or implementation, but a human owns the final design, review, security decision, and test result.

## Repository layout

| Folder | Purpose |
| --- | --- |
| `src/` | API and application code |
| `tests/` | Automated tests that protect behaviour |
| `experiments/` | Short-lived spikes and experiments; record the question and result |
| `evaluations/` | Repeatable evaluations for quality, correctness, safety, and AI output |
| `docs/` | Design notes, decisions, API contracts, and learning notes |

## Run it

```powershell
dotnet restore AiEngineeringLab.slnx --ignore-failed-sources
dotnet run --project src/FoundationalModel.API
```

Then visit:

- `GET /api/v1/system/health`
- `GET /api/v1/system/info`

## Test it

```powershell
dotnet test AiEngineeringLab.slnx --no-restore
```

## Working with AI

1. Write the problem and acceptance criteria in `docs/` before asking an AI tool to code.
2. Keep experiments isolated from production code until their result is understood.
3. Ask the model to explain assumptions, risks, and changed files.
4. Review every generated change for correctness, security, privacy, maintainability, and licensing.
5. Add or update tests before calling the work complete.
6. Record what worked and what failed in `evaluations/`.

## Current API contract

The API currently exposes a health endpoint and a basic service-information endpoint. They are intentionally small starting points for exercises involving validation, persistence, authentication, observability, integrations, and AI-assisted refactoring.
