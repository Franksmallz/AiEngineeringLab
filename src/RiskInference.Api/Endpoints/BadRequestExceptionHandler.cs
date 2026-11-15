using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RiskInference.Api.Endpoints;

// ASP.NET Core throws BadHttpRequestException when the body is empty, is not
// JSON, or has the wrong type for a field. Without this handler the general
// exception handler would turn that client error into a 500.
// The response is a fixed 400 message. The framework message is only logged.
public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    private readonly ILogger<BadRequestExceptionHandler> _logger;

    public BadRequestExceptionHandler(ILogger<BadRequestExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException)
            return false;

        _logger.LogInformation("Rejected an unreadable request: {Reason}", exception.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request body is not valid.",
            Detail = "Send a JSON object with the six transaction fields."
        }, cancellationToken);
        return true;
    }
}
