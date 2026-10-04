using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SlseaSolarApi.Api.Application.Common;
using SlseaSolarApi.Api.Domain.Exceptions;

namespace SlseaSolarApi.Api.Presentation.Filters;

/// <summary>
/// Converts every exception into the one <see cref="ApiError"/> body (design decision D10), so no
/// controller ever hand-rolls an error response.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description>Known <see cref="ApiException"/> subclasses map to their declared status code.</description></item>
///   <item><description>Model-binding failures produce a 400 (<c>MALFORMED_REQUEST</c>).</description></item>
///   <item><description>Anything else is a 500 (<c>INTERNAL_ERROR</c>) with the detail suppressed.</description></item>
/// </list>
/// </remarks>
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public void OnException(ExceptionContext context)
    {
        var traceId = context.HttpContext.TraceIdentifier;

        var (statusCode, error) = context.Exception switch
        {
            ValidationException validation => (
                validation.StatusCode,
                new ApiError
                {
                    Code = validation.Code,
                    Message = validation.Message,
                    Detail = validation.Detail,
                    Errors = validation.Errors.Count > 0 ? validation.Errors : null,
                    TraceId = traceId
                }),

            ApiException api => (
                api.StatusCode,
                new ApiError
                {
                    Code = api.Code,
                    Message = api.Message,
                    Detail = api.Detail,
                    TraceId = traceId
                }),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An unexpected error occurred while processing the request.",
                    // The detail is only surfaced in Development so production never leaks internals.
                    Detail = _environment.IsDevelopment() ? context.Exception.ToString() : null,
                    TraceId = traceId
                })
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(context.Exception, "Unhandled exception. TraceId={TraceId}", traceId);
        }
        else
        {
            _logger.LogInformation(
                "Handled {Code} -> {Status}. TraceId={TraceId}",
                error.Code, statusCode, traceId);
        }

        context.Result = new ObjectResult(error) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }
}