using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RiskInference.Api.Operations;

namespace RiskInference.Api.Endpoints;

// ASP.NET Core throws BadHttpRequestException when the body is empty, is not
// JSON, or has the wrong type for a field. Without this handler the general
// exception handler would turn that client error into a 500.
// The response is a fixed message and the request ID. The framework message is only logged.
public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    private readonly ILogger<BadRequestExceptionHandler> _logger;

    public BadRequestExceptionHandler(ILogger<BadRequestExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException bad)
            return false;

        _logger.LogInformation("Rejected an unreadable request. RequestId {RequestId} Reason {Reason}",
            httpContext.TraceIdentifier, bad.Message);

        httpContext.SetOutcome(RequestOutcome.MalformedRequest);
        httpContext.Response.StatusCode = bad.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = bad.StatusCode,
            Title = "The request body is not valid.",
            Detail = "Send a JSON object with the six transaction fields.",
            Extensions = { ["requestId"] = httpContext.TraceIdentifier }
        }, cancellationToken);
        return true;
    }
}
