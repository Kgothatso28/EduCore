using EduCore.Api.Security;

namespace EduCore.Api.Security;

/// <summary>Validates authenticated school claims before tenant-aware request processing.</summary>
public sealed class SchoolTenantMiddleware(RequestDelegate next)
{
    /// <summary>Blocks stale or unauthorized tenant claims while leaving auth routes available for recovery.</summary>
    public async Task InvokeAsync(HttpContext context, SchoolContextResolver resolver)
    {
        // Auth routes validate school access themselves; a stale school claim must not prevent recovery.
        var isAuthenticationRoute = context.Request.Path.StartsWithSegments("/api/v1/auth");
        // Platform endpoints are authorized against platform grants and never establish school scope.
        var isPlatformRoute = context.Request.Path.StartsWithSegments("/api/v1/platform");
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(claim => claim.Type == SchoolContextResolver.ActiveSchoolClaim) &&
            !await resolver.TryEstablishAsync(context.User, context.RequestAborted) &&
            !isAuthenticationRoute &&
            !isPlatformRoute)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
