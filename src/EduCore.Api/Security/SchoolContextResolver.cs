using System.Security.Claims;
using EduCore.Api.Data;
using EduCore.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Security;

/// <summary>Resolves a claimed school against current database membership and school status.</summary>
public sealed class SchoolContextResolver(
    EduCoreDbContext dbContext,
    TenantContext tenantContext)
{
    public const string ActiveSchoolClaim = "active_school_id";

    /// <summary>Establishes a request-local tenant only when identity and active membership are unambiguous.</summary>
    public async Task<bool> TryEstablishAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userIds = principal.FindAll(ClaimTypes.NameIdentifier).Select(claim => claim.Value)
            .ToArray();
        var schoolIds = principal.FindAll(ActiveSchoolClaim).Select(claim => claim.Value).ToArray();
        if (userIds.Length != 1 || schoolIds.Length != 1 ||
            !Guid.TryParse(schoolIds[0], out var schoolId))
        {
            return false;
        }

        // Treat the signed-in school claim as a request; membership and school status remain authoritative in the database.
        var role = await dbContext.SchoolMemberships
            .AsNoTracking()
            .Where(membership =>
                    membership.SchoolId == schoolId &&
                    membership.SubjectId == userIds[0] &&
                    membership.IsActive)
            .Select(membership => (SchoolRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (!role.HasValue || !await dbContext.Schools
                .AnyAsync(school =>
                    school.Id == schoolId && school.Status == SchoolStatus.Active,
                    cancellationToken))
        {
            return false;
        }

        tenantContext.Establish(schoolId, role.Value);
        return true;
    }
}
