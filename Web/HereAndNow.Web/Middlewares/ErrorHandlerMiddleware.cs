using HereAndNowService.Exceptions;

namespace HereAndNowService.Middlewares;

class ErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlerMiddleware> _logger;

    public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            if (context.Response is HttpResponse response && response.StatusCode == 404)
            {
                await response.WriteAsJsonAsync(new {
                    message = "Not Found"
                });
            }
            else if (context.Response is HttpResponse unauthorizedResponse && unauthorizedResponse.StatusCode == 401)
            {
                await unauthorizedResponse.WriteAsJsonAsync(
                    new {
                        message = context.Request.Headers.ContainsKey("Authorization")
                                        ? "Bad credentials"
                                        : "Requires authentication"
                    });
            }
        }
        catch (Exception ex)
        {
            await HandleException(context, ex);
        }
    }

    private async Task HandleException(HttpContext context, Exception ex)
    {
        // Handle service unavailability (e.g., Cosmos DB down)
        if (ex is ServiceUnavailableException serviceEx)
        {
            _logger.LogError(ex, "Service unavailable: {ServiceName}", serviceEx.ServiceName);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new {
                message = "Service temporarily unavailable. Please try again later."
            });
            return;
        }

        // Handle all other exceptions as 500
        _logger.LogError(ex, "Unhandled exception occurred");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new {
            message = "Internal Server Error."
        });
    }
}

public static class ErrorHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlerMiddleware>();
    }
}
