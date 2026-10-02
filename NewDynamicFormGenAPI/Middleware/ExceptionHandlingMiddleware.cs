using System.Net;
using System.Text.Json;

namespace NewDynamicFormGenAPI.API.Middleware;

/// <summary>
/// ASP.NET Core middleware for global exception handling and error response standardization.
/// </summary>
/// <remarks>
/// <para>
/// ExceptionHandlingMiddleware catches unhandled exceptions occurring in the request pipeline
/// and returns a standardized error response to the client. This ensures:
/// <list type="bullet">
///   <item><description>Consistent error response format across all endpoints</description></item>
///   <item><description>Proper HTTP status codes for different error scenarios</description></item>
///   <item><description>Logging of all exceptions for diagnostics and monitoring</description></item>
///   <item><description>Preventing sensitive error details from leaking to clients</description></item>
/// </list>
/// </para>
/// <para>
/// This middleware should be registered near the beginning of the pipeline in Program.cs
/// to catch exceptions from all downstream middleware and handlers.
/// </para>
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The delegate representing the next middleware in the pipeline.</param>
    /// <param name="logger">The logger for recording exception information.</param>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware, wrapping the next middleware in a try-catch block.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <remarks>
    /// If an exception occurs during processing of the request, this method:
    /// <list type="bullet">
    ///   <item><description>Logs the exception with context information</description></item>
    ///   <item><description>Sets the response to 500 Internal Server Error</description></item>
    ///   <item><description>Returns a standardized JSON error response</description></item>
    /// </list>
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var payload = JsonSerializer.Serialize(new
            {
                success = false,
                message = "Something went wrong.",
                errors = new[] { ex.Message }
            });

            await context.Response.WriteAsync(payload);
        }
    }
}
