using System.Security.Claims;
using EduCore.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Security;

/// <summary>Requires a current database grant for a single platform capability.</summary>
public sealed class PlatformPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Resolves platform grants from SQL for every request instead of trusting client claims.</summary>
public sealed class PlatformPermissionHandler(EduCoreDbContext dbContext)
    : AuthorizationHandler<PlatformPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformPermissionRequirement requirement)
    {
        var subjectId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated == true &&
            !string.IsNullOrWhiteSpace(subjectId) &&
            await dbContext.PlatformAdministratorGrants.AnyAsync(
                grant => grant.SubjectId == subjectId &&
                         grant.Permission == requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
