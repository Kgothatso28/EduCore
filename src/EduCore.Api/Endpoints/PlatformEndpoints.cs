using System.Security.Claims;
using System.Net;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using EduCore.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Endpoints;

/// <summary>Maps capability-protected platform administration APIs.</summary>
public static class PlatformEndpoints
{
    /// <summary>Registers operations over platform metadata, never tenant academic records.</summary>
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform");

        group.MapGet("/dashboard", async (
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
            Results.Ok(await administration.GetDashboardAsync(cancellationToken)))
            .RequireAuthorization(PlatformPermissions.DashboardView);

        group.MapGet("/schools", async (
            string? search,
            string? status,
            string? sort,
            bool? descending,
            int? page,
            int? pageSize,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            if (!ValidPage(page ?? 1, pageSize ?? 20) ||
                (search?.Length > 200) ||
                (status is not null && !Enum.TryParse<SchoolStatus>(status, true, out _)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["query"] = ["Invalid school search, status, or pagination values."]
                });
            }

            SchoolStatus? parsedStatus = status is null
                ? null : Enum.Parse<SchoolStatus>(status, true);
            return Results.Ok(await administration.GetSchoolsAsync(
                search, parsedStatus, ValidSort(sort), descending ?? false,
                page ?? 1, pageSize ?? 20, cancellationToken));
        }).RequireAuthorization(PlatformPermissions.SchoolsView);

        group.MapGet("/schools/{schoolId:guid}", async (
            Guid schoolId,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            var details = await administration.GetSchoolAsync(schoolId, cancellationToken);
            return details is null ? Results.NotFound() : Results.Ok(details);
        }).RequireAuthorization(PlatformPermissions.SchoolsView);

        group.MapPost("/schools", async (
            CreateSchoolRequest request,
            ClaimsPrincipal user,
            EduCore.Api.Data.EduCoreDbContext dbContext,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            var name = request.Name?.Trim();
            var slug = request.Slug?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
                !ValidSlug(slug))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["school"] = ["A name and a 1-80 character URL-safe slug are required."]
                });
            }

            var normalizedSlug = slug!.ToLowerInvariant();
            if (await dbContext.Schools.AnyAsync(
                    school => school.Slug == normalizedSlug, cancellationToken))
            {
                return Results.Conflict(new { error = "That school identifier is already registered." });
            }

            var actor = GetSubjectId(user);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            var created = await administration.CreateSchoolAsync(
                actor, name, normalizedSlug, cancellationToken);
            return Results.Created($"/api/v1/platform/schools/{created.Id}", created);
        }).RequireAuthorization(PlatformPermissions.SchoolsManage);

        group.MapPut("/schools/{schoolId:guid}/status", async (
            Guid schoolId,
            SetSchoolStatusRequest request,
            ClaimsPrincipal user,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<SchoolStatus>(request.Status, true, out var status) ||
                status == SchoolStatus.Archived)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Status must be Active or Suspended."]
                });
            }

            var actor = GetSubjectId(user);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return await administration.SetSchoolStatusAsync(
                actor, schoolId, status, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization(PlatformPermissions.SchoolsManage);

        group.MapGet("/users", async (
            string? search,
            int? page,
            int? pageSize,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            if (!ValidPage(page ?? 1, pageSize ?? 20) || search?.Length > 200)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["query"] = ["Invalid user search or pagination values."]
                });
            }

            return Results.Ok(await administration.GetUsersAsync(
                search, page ?? 1, pageSize ?? 20, cancellationToken));
        }).RequireAuthorization(PlatformPermissions.UsersView);

        group.MapPut("/memberships/{membershipId:guid}", async (
            Guid membershipId,
            UpdateMembershipRequest request,
            ClaimsPrincipal user,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<SchoolRole>(request.Role, true, out var role) ||
                !Enum.IsDefined(role))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = ["A valid school-level role is required."]
                });
            }

            var actor = GetSubjectId(user);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return await administration.UpdateMembershipAsync(
                actor, membershipId, role, request.IsActive, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization(PlatformPermissions.UsersManage);

        group.MapGet("/memberships/{membershipId:guid}", async (
            Guid membershipId,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            var membership = await administration.GetMembershipAsync(
                membershipId, cancellationToken);
            return membership is null ? Results.NotFound() : Results.Ok(membership);
        }).RequireAuthorization(PlatformPermissions.UsersView);

        group.MapPost("/schools/{schoolId:guid}/memberships", async (
            Guid schoolId,
            CreateMembershipRequest request,
            ClaimsPrincipal user,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            var subjectId = request.SubjectId?.Trim();
            if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Length > 200 ||
                !Enum.TryParse<SchoolRole>(request.Role, true, out var role) ||
                !Enum.IsDefined(role))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["membership"] = ["A valid trusted subject identifier and school role are required."]
                });
            }

            var actor = GetSubjectId(user);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            var result = await administration.CreateMembershipAsync(
                actor, schoolId, subjectId, role, cancellationToken);
            return result.Status switch
            {
                PlatformAdministrationService.MembershipCreateStatus.Created =>
                    Results.Created(
                        $"/api/v1/platform/memberships/{result.MembershipId}",
                        new { id = result.MembershipId }),
                PlatformAdministrationService.MembershipCreateStatus.SchoolNotFound =>
                    Results.NotFound(),
                PlatformAdministrationService.MembershipCreateStatus.AlreadyExists =>
                    Results.Conflict(new { error = "That identity already has a membership in this school." }),
                _ => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["subjectId"] = ["Administrators cannot assign school roles to themselves."]
                })
            };
        }).RequireAuthorization(PlatformPermissions.UsersManage);

        group.MapPost("/users/{subjectId}/password-recovery", async (
            string subjectId,
            ClaimsPrincipal user,
            KeycloakIdentityManagementService identityManagement,
            EduCore.Api.Data.EduCoreDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            return await RunIdentityActionAsync(
                "identity.password_recovery",
                subjectId,
                user,
                identityManagement.IsConfigured,
                ct => identityManagement.SendPasswordRecoveryAsync(subjectId, ct),
                dbContext,
                loggerFactory.CreateLogger("EduCore.PlatformIdentity"),
                cancellationToken);
        })
        .RequireAuthorization(PlatformPermissions.IdentityManage)
        .RequireRateLimiting("identity-action");

        group.MapPost("/users/{subjectId}/mfa-enrollment", async (
            string subjectId,
            ClaimsPrincipal user,
            KeycloakIdentityManagementService identityManagement,
            EduCore.Api.Data.EduCoreDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            return await RunIdentityActionAsync(
                "identity.mfa_enrollment",
                subjectId,
                user,
                identityManagement.IsConfigured,
                ct => identityManagement.RequireTotpEnrollmentAsync(subjectId, ct),
                dbContext,
                loggerFactory.CreateLogger("EduCore.PlatformIdentity"),
                cancellationToken);
        })
        .RequireAuthorization(PlatformPermissions.IdentityManage)
        .RequireRateLimiting("identity-action");

        group.MapPost("/users/{subjectId}/sessions/revoke", async (
            string subjectId,
            ClaimsPrincipal user,
            KeycloakIdentityManagementService identityManagement,
            EduCore.Api.Data.EduCoreDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            return await RunIdentityActionAsync(
                "identity.sessions.revocation",
                subjectId,
                user,
                identityManagement.IsConfigured,
                ct => identityManagement.RevokeSessionsAsync(subjectId, ct),
                dbContext,
                loggerFactory.CreateLogger("EduCore.PlatformIdentity"),
                cancellationToken);
        })
        .RequireAuthorization(PlatformPermissions.IdentityManage)
        .RequireRateLimiting("identity-action");

        group.MapGet("/permissions", async (
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
            Results.Ok(await administration.GetGrantsAsync(cancellationToken)))
            .RequireAuthorization(PlatformPermissions.RolesView);

        group.MapPost("/permissions", async (
            GrantPermissionRequest request,
            ClaimsPrincipal user,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            var subjectId = request.SubjectId;
            if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Length > 200 ||
                string.IsNullOrWhiteSpace(request.Permission))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["permission"] = ["A valid user identifier and permission are required."]
                });
            }

            var actor = GetSubjectId(user);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return await administration.GrantPermissionAsync(
                actor, subjectId, request.Permission, cancellationToken)
                ? Results.NoContent()
                : Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["permission"] =
                    [
                        "The permission is not delegable, the target is unknown, or self-grants are prohibited."
                    ]
                });
        }).RequireAuthorization(PlatformPermissions.RolesManage);

        group.MapDelete("/permissions/{grantId:guid}", async (
            Guid grantId,
            ClaimsPrincipal user,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            var actor = GetSubjectId(user);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return await administration.RevokePermissionAsync(actor, grantId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization(PlatformPermissions.RolesManage);

        group.MapGet("/audit-events", async (
            string? search,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? page,
            int? pageSize,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            if (!ValidHistoryQuery(search, fromUtc, toUtc, page ?? 1, pageSize ?? 50))
            {
                return InvalidHistoryQuery();
            }

            return Results.Ok(await administration.GetAuditEventsAsync(
                search, fromUtc, toUtc, page ?? 1, pageSize ?? 50, cancellationToken));
        }).RequireAuthorization(PlatformPermissions.AuditView);

        group.MapGet("/login-history", async (
            string? search,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? page,
            int? pageSize,
            PlatformAdministrationService administration,
            CancellationToken cancellationToken) =>
        {
            if (!ValidHistoryQuery(search, fromUtc, toUtc, page ?? 1, pageSize ?? 50))
            {
                return InvalidHistoryQuery();
            }

            return Results.Ok(await administration.GetLoginEventsAsync(
                search, fromUtc, toUtc, page ?? 1, pageSize ?? 50, cancellationToken));
        }).RequireAuthorization(PlatformPermissions.AuditView);

        return endpoints;
    }

    /// <summary>Rejects unreasonable page sizes and offsets at the HTTP boundary.</summary>
    private static bool ValidPage(int page, int pageSize) =>
        page > 0 && pageSize is > 0 and <= 100 && page <= 1_000_000;

    /// <summary>Restricts school ordering to explicit known database fields.</summary>
    private static string ValidSort(string? sort) =>
        sort is "created" or "status" ? sort : "name";

    /// <summary>Accepts only URL-safe ASCII school identifiers.</summary>
    private static bool ValidSlug(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) &&
        slug.Length <= 80 &&
        slug.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    /// <summary>Validates history search, date range, and bounded pagination.</summary>
    private static bool ValidHistoryQuery(
        string? search,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize) =>
        (search is null || search.Length <= 200) &&
        (!fromUtc.HasValue || !toUtc.HasValue || fromUtc < toUtc) &&
        ValidPage(page, pageSize);

    /// <summary>Builds a consistent validation response for invalid history filters.</summary>
    private static IResult InvalidHistoryQuery() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["query"] = ["Invalid search, UTC time range, or pagination values."]
        });

    private static async Task<IResult> RunIdentityActionAsync(
        string action,
        string subjectId,
        ClaimsPrincipal user,
        bool providerConfigured,
        Func<CancellationToken, Task> operation,
        EduCore.Api.Data.EduCoreDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var actor = GetSubjectId(user);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Length > 200)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subjectId"] = ["A valid identity-provider subject is required."]
            });
        }

        if (!providerConfigured)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Identity management is not configured",
                detail: "Configure the Keycloak identity-management integration before using this operation.");
        }

        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            actor, $"{action}.requested", "identity", subjectId, null));
        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
        }
        catch (KeycloakIdentityManagementException exception)
        {
            logger.LogError(
                "Keycloak identity operation {Action} failed with HTTP status {StatusCode}.",
                action, (int)exception.StatusCode);
            dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
                actor, $"{action}.failed", "identity", subjectId,
                $"Provider status={(int)exception.StatusCode}."));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Identity provider operation failed",
                detail: "Keycloak did not complete the requested identity operation.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Keycloak identity operation {Action} could not reach the provider.", action);
            dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
                actor, $"{action}.failed", "identity", subjectId, "Provider connection failed."));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Identity provider is unavailable",
                detail: "Keycloak could not be reached to complete the requested operation.");
        }

        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            actor, $"{action}.completed", "identity", subjectId, null));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Accepted(value: new { status = "accepted" });
    }

    /// <summary>Returns one unambiguous trusted subject claim from the authenticated principal.</summary>
    private static string? GetSubjectId(ClaimsPrincipal user)
    {
        var subjectIds = user.FindAll(ClaimTypes.NameIdentifier).Select(claim => claim.Value).ToArray();
        return subjectIds.Length == 1 && subjectIds[0].Length <= 200 ? subjectIds[0] : null;
    }

    public sealed record CreateSchoolRequest(string? Name, string? Slug);
    public sealed record SetSchoolStatusRequest(string Status);
    public sealed record UpdateMembershipRequest(string Role, bool IsActive);
    public sealed record GrantPermissionRequest(string SubjectId, string Permission);
    public sealed record CreateMembershipRequest(string? SubjectId, string? Role);
}
