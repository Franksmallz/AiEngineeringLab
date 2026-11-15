using RiskInference.Api;

// Ctrl+C or SIGTERM stops the host gracefully. See RiskApiHost.
var app = RiskApiHost.Build(args);
await app.RunAsync();
