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

    /// <summary>Adds identity-management capability to an existing trusted platform security admin.</summary>
    public async Task ProvisionIdentityManagementCapabilityAsync(
        CancellationToken cancellationToken)
    {
        var subjectId = configuration["PlatformAdmin:BootstrapSubjectId"]?.Trim();
        if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Length > 200)
        {
            throw new InvalidOperationException(
                "PlatformAdmin:BootstrapSubjectId must contain the trusted identity-provider subject.");
        }

        if (!await dbContext.PlatformAdministratorGrants.AnyAsync(
                item => item.SubjectId == subjectId &&
                        item.Permission == PlatformPermissions.RolesManage,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The configured subject must already hold platform.roles.manage.");
        }

        if (await dbContext.PlatformAdministratorGrants.AnyAsync(
                item => item.SubjectId == subjectId &&
                        item.Permission == PlatformPermissions.IdentityManage,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The configured subject already has platform.identity.manage.");
        }

        dbContext.PlatformAdministratorGrants.Add(new PlatformAdministratorGrant(
            subjectId, PlatformPermissions.IdentityManage));
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            "system.bootstrap", "platform.identity.manage.provisioned", "identity",
            subjectId, "Identity-management capability was provisioned for an existing security administrator."));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
