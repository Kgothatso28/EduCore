using System.Security.Claims;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using EduCore.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EduCore.Api.Endpoints;

/// <summary>Maps the Development-only sign-in and active-school session routes.</summary>
public static class DemoAuthenticationEndpoints
{
    /// <summary>Registers cookie-authenticated routes used by the local demonstration UI.</summary>
    public static IEndpointRouteBuilder MapDemoAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // Issue the request token consumed by the client on later state-changing requests.
        endpoints.MapGet("/api/v1/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        // Verify demo credentials before issuing a cookie with only the identity subject/name.
        endpoints.MapPost("/api/v1/auth/login", async (
            DemoLoginRequest request,
            HttpContext context,
            DemoAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "Username and password are required." });
            }

            var credential = await authentication.VerifyAsync(
                request.UserName, request.Password, cancellationToken);
            if (credential is null)
            {
                return Results.Unauthorized();
            }

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                DemoAuthenticationService.CreatePrincipal(credential));
            return Results.Ok(new { userName = credential.UserName });
        }).AllowAnonymous();

        // List only active schools for which the authenticated subject currently has membership.
        endpoints.MapGet("/api/v1/auth/schools", async (
            ClaimsPrincipal user,
            DemoAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (subjectId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await authentication.GetSchoolsAsync(subjectId, cancellationToken));
        }).RequireAuthorization();

        // Recheck membership before signing an active-school claim; the claim itself is not authority.
        endpoints.MapPost("/api/v1/auth/school", async (
            DemoSchoolSelection request,
            ClaimsPrincipal user,
            HttpContext context,
            DemoAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (subjectId is null ||
                !await authentication.CanAccessSchoolAsync(
                    subjectId, request.SchoolId, cancellationToken))
            {
                return Results.NotFound();
            }

            var userName = user.Identity?.Name ?? subjectId;
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, subjectId),
                new Claim(ClaimTypes.Name, userName),
                new Claim(SchoolContextResolver.ActiveSchoolClaim, request.SchoolId.ToString())
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name, ClaimTypes.Role));
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return Results.NoContent();
        }).RequireAuthorization();

        // Clear the demo authentication cookie so a stale school selection can be recovered.
        endpoints.MapPost("/api/v1/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        // Return session details only after middleware has established a current school membership.
        endpoints.MapGet("/api/v1/session", async (
            ClaimsPrincipal user,
            TenantContext tenantContext,
            EduCore.Api.Data.EduCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var schoolId = tenantContext.SchoolId;
            if (!schoolId.HasValue)
            {
                return Results.Forbid();
            }

            return await SessionResponseAsync(user, tenantContext, dbContext, cancellationToken);
        }).RequireAuthorization("SchoolMember");

        return endpoints;
    }

    /// <summary>Captures the credentials submitted to the local demo sign-in route.</summary>
    public sealed record DemoLoginRequest(string UserName, string Password);

    /// <summary>Selects a school to be revalidated and stored in the authenticated session.</summary>
    public sealed record DemoSchoolSelection(Guid SchoolId);

    /// <summary>Builds the current session response from the validated school context.</summary>
    private static async Task<IResult> SessionResponseAsync(
        ClaimsPrincipal user,
        TenantContext tenantContext,
        EduCore.Api.Data.EduCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var school = await dbContext.Schools.FindAsync(
            [tenantContext.SchoolId!.Value], cancellationToken);
        if (school is null)
        {
            return Results.Forbid();
        }

        return Results.Ok(new
        {
            userName = user.Identity?.Name,
            subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            schoolId = school.Id,
            schoolName = school.Name,
            role = tenantContext.Role
        });
    }
}
