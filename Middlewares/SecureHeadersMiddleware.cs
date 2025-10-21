namespace App.Middlewares;

class SecureHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecureHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-XSS-Protection"] = "0";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        context.Response.Headers["X-Frame-Options"] = "deny";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none';";
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, max-age=0, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";

        await _next(context);
    }
}

public static class SecureHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecureHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecureHeadersMiddleware>();
    }
}
