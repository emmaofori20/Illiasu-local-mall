using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ILLiasu_Local_Mall.Infrastructure.Logging;

public sealed class RequestOutcomeLogging(RequestDelegate next, ILogger<RequestOutcomeLogging> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Server-generated; ignore any client-supplied correlation headers.
        var requestId = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = requestId;
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId });
        var started = Stopwatch.GetTimestamp();
        Exception? escaped = null;
        try { await next(context); }
        catch (Exception error) { escaped = error; throw; }
        finally
        {
            try
            {
                var feature = context.Features.Get<IExceptionHandlerFeature>();
                var error = escaped ?? feature?.Error;
                var action = (feature?.Endpoint ?? context.GetEndpoint())?.Metadata.GetMetadata<ControllerActionDescriptor>();
                var endpoint = action == null ? "Unmatched" : $"{action.ControllerName}.{action.ActionName}";
                var method = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" }.Contains(context.Request.Method)
                    ? context.Request.Method : "OTHER";
                var aborted = context.RequestAborted.IsCancellationRequested;
                if (error != null && !aborted)
                    logger.LogError(error, "Diagnostic {DiagnosticEvent} {Endpoint}", "UnhandledRequestException", endpoint);
                var status = aborted ? 499 : escaped != null ? 500 : context.Response.StatusCode;
                logger.Log(status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information,
                    "Diagnostic {DiagnosticEvent} {Method} {Endpoint} {StatusCode} {ElapsedMilliseconds} {Outcome}",
                    "HttpRequestCompleted", method, endpoint, status,
                    Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2), aborted ? "Aborted" : status >= 400 ? "Failed" : "Succeeded");
            }
            catch { ApplicationLogging.ReportSinkFailure(); }
        }
    }
}
