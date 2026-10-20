using EduCore.Api.Data;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Services;

/// <summary>Performs one explicit first-platform-admin grant from trusted deployment configuration.</summary>
public sealed class PlatformAdminBootstrapper(
    EduCoreDbContext dbContext,
    IConfiguration configuration)
{
    /// <summary>Grants bootstrap capabilities once; repeated use cannot silently restore a revoked root.</summary>
    public async Task ProvisionFirstAdministratorAsync(CancellationToken cancellationToken)
    {
        var subjectId = configuration["PlatformAdmin:BootstrapSubjectId"]?.Trim();
        if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Length > 200)
        {
            throw new InvalidOperationException(
                "PlatformAdmin:BootstrapSubjectId must contain the trusted identity-provider subject.");
        }

        if (await dbContext.PlatformAdministratorGrants.AnyAsync(
                item => item.Permission == PlatformPermissions.RolesManage,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "A platform security administrator is already provisioned.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.PlatformAdministratorGrants.AddRange(
            PlatformPermissions.Bootstrap.Select(permission =>
                new PlatformAdministratorGrant(subjectId, permission)));
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            "system.bootstrap", "platform.admin.provisioned", "identity", subjectId,
            "Initial platform capabilities were provisioned from trusted deployment configuration."));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
