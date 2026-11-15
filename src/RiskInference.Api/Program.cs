using RiskInference.Api;

// Ctrl+C or SIGTERM stops the host gracefully. See RiskApiHost.
try
{
    var app = RiskApiHost.Build(args);
    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    // A bad manifest, model or configuration ends here. Print one clear line
    // and exit with code 1. An unhandled exception would end the process as a crash
    // (on Linux, code 134 or 139, often with a core dump).
    Console.Error.WriteLine($"STARTUP FAILED: {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}
