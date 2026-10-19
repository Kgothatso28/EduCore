using Microsoft.AspNetCore.Authorization;
using EduCore.Api.Domain;

namespace EduCore.Api.Security;

/// <summary>Requires an authenticated principal with a validated school membership and optional role.</summary>
public sealed class ActiveSchoolMembershipRequirement(SchoolRole? requiredRole = null)
    : IAuthorizationRequirement
{
    /// <summary>Optional role the validated membership must hold to satisfy this policy.</summary>
    public SchoolRole? RequiredRole { get; } = requiredRole;
}

/// <summary>Authorizes only the role already validated for the current school context.</summary>
public sealed class ActiveSchoolMembershipHandler(TenantContext tenantContext)
    : AuthorizationHandler<ActiveSchoolMembershipRequirement>
{
    /// <summary>Succeeds when authentication, validated tenant context, and required role all match.</summary>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveSchoolMembershipRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            tenantContext.IsValidated &&
            (requirement.RequiredRole is null || tenantContext.Role == requirement.RequiredRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
