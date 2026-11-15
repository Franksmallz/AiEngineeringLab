using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RiskInference.Api.Model;
using RiskInference.Api.Operations;

namespace RiskInference.Api.Endpoints;

// The last handler. Any error that no other handler knows becomes a 500.
//
// The client gets a fixed message and the request ID. It never gets the
// exception type, the message, a stack trace or a file path.
// The log gets the full exception, with the request ID and the versions, so
// that an operator can find the cause from the ID that the client reports.
public sealed partial class UnexpectedErrorHandler : IExceptionHandler
{
    private readonly ILogger<UnexpectedErrorHandler> _logger;
    private readonly IRiskModelService _model;

    public UnexpectedErrorHandler(ILogger<UnexpectedErrorHandler> logger, IRiskModelService model)
    {
        _logger = logger;
        _model = model;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnexpected(exception, httpContext.TraceIdentifier, _model.Active.ModelVersion, _model.Active.DecisionPolicyVersion);

        httpContext.SetOutcome(RequestOutcome.Error);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Report the request ID to the operator of the service.",
            Extensions = { ["requestId"] = httpContext.TraceIdentifier }
        }, cancellationToken);
        return true;
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Error,
        Message = "Unexpected error. RequestId {RequestId} ModelVersion {ModelVersion} PolicyVersion {PolicyVersion}")]
    private partial void LogUnexpected(Exception exception, string requestId, string modelVersion, string policyVersion);
}
