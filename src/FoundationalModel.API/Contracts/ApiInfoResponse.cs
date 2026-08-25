namespace FoundationalModel.API.Contracts;

public sealed record ApiInfoResponse(
    string Name,
    string Version,
    string Environment,
    DateTimeOffset UtcTime);
