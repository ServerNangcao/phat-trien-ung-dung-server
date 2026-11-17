using Ticketing.Api.Middlewares;

namespace Ticketing.Api.Extensions;

/// <summary>
/// Extension method để đăng ký SecurityHeadersMiddleware vào pipeline.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}

