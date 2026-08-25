# Architecture

The API is an ASP.NET Core 8 controller-based service. The first slice contains:

- `Program.cs` for dependency registration and the HTTP pipeline;
- `Controllers/SystemController.cs` for operational endpoints;
- `Contracts/ApiInfoResponse.cs` for an explicit response contract;
- a test project that protects the response contract.

New capabilities should be introduced behind a clear application boundary. Controllers should translate HTTP requests and responses; domain decisions belong in application services or domain objects; infrastructure access belongs behind interfaces.
