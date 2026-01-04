using HereAndNowService.DTOs;

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

            // Only write default messages if the response hasn't already been written
            if (context.Response.HasStarted)
            {
                return;
            }

            if (context.Response is HttpResponse response && response.StatusCode == 404)
            {
                await response.WriteAsJsonAsync(new ErrorResponseDto
                {
                    Error = new ErrorDetailsDto
                    {
                        Code = "NOT_FOUND",
                        Message = "Not Found"
                    }
                });
            }
            else if (context.Response is HttpResponse unauthorizedResponse && unauthorizedResponse.StatusCode == 401)
            {
                await unauthorizedResponse.WriteAsJsonAsync(new ErrorResponseDto
                {
                    Error = new ErrorDetailsDto
                    {
                        Code = "UNAUTHORIZED",
                        Message = context.Request.Headers.ContainsKey("Authorization")
                            ? "Bad credentials"
                            : "Requires authentication"
                    }
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
        _logger.LogError(ex, "Unhandled exception occurred while processing {Method} {Path}",
            context.Request.Method, context.Request.Path);

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto
            {
                Error = new ErrorDetailsDto
                {
                    Code = "INTERNAL_ERROR",
                    Message = "Internal Server Error"
                }
            });
        }
    }
}

public static class ErrorHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlerMiddleware>();
    }
}
