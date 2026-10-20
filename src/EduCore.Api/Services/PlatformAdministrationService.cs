using EduCore.Api.Data;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Services;

/// <summary>Reads and changes platform metadata without querying tenant academic records.</summary>
public sealed class PlatformAdministrationService(EduCoreDbContext dbContext)
{
    /// <summary>Builds aggregate platform metrics without including student-level information.</summary>
    public async Task<PlatformDashboard> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return new PlatformDashboard(
            await dbContext.Schools.CountAsync(cancellationToken),
            await dbContext.Schools.CountAsync(
                school => school.Status == SchoolStatus.Active, cancellationToken),
            await dbContext.Schools.CountAsync(
                school => school.Status == SchoolStatus.Suspended, cancellationToken),
            await dbContext.SchoolMemberships.Select(item => item.SubjectId)
                .Distinct().CountAsync(cancellationToken),
            await dbContext.SchoolMemberships.Where(item => item.IsActive)
                .Select(item => item.SubjectId).Distinct().CountAsync(cancellationToken),
            await dbContext.PlatformLoginEvents.CountAsync(
                item => item.CreatedUtc >= now.AddDays(-1) && item.Succeeded, cancellationToken),
            await dbContext.PlatformLoginEvents.CountAsync(
                item => item.CreatedUtc >= now.AddDays(-1) && !item.Succeeded, cancellationToken),
            await dbContext.PlatformAuditEvents.CountAsync(
                item => item.CreatedUtc >= now.AddDays(-7), cancellationToken),
            await dbContext.Database.CanConnectAsync(cancellationToken));
    }

    /// <summary>Returns a filtered, ordered school page with membership aggregates only.</summary>
    public async Task<PageResult<SchoolSummary>> GetSchoolsAsync(
        string? search,
        SchoolStatus? status,
        string sort,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<School> query = dbContext.Schools.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(school =>
                school.Name.Contains(term) || school.Slug.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(school => school.Status == status.Value);
        }

        query = (sort, descending) switch
        {
            ("created", false) => query.OrderBy(school => school.CreatedUtc),
            ("created", true) => query.OrderByDescending(school => school.CreatedUtc),
            ("status", false) => query.OrderBy(school => school.Status).ThenBy(school => school.Name),
            ("status", true) => query.OrderByDescending(school => school.Status)
                .ThenBy(school => school.Name),
            (_, false) => query.OrderBy(school => school.Name),
            _ => query.OrderByDescending(school => school.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(school => new SchoolSummary(
                school.Id,
                school.Name,
                school.Slug,
                school.Status,
                school.CreatedUtc,
                dbContext.SchoolMemberships.Count(
                    membership => membership.SchoolId == school.Id)))
            .ToListAsync(cancellationToken);
        return new PageResult<SchoolSummary>(items, totalCount, page, pageSize);
    }

    /// <summary>Returns one school's non-academic profile and role-count aggregates.</summary>
    public async Task<SchoolDetails?> GetSchoolAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var school = await dbContext.Schools.AsNoTracking()
            .Where(item => item.Id == schoolId)
            .Select(item => new { item.Id, item.Name, item.Slug, item.Status, item.CreatedUtc })
            .SingleOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            return null;
        }

        var memberships = await dbContext.SchoolMemberships.AsNoTracking()
            .Where(item => item.SchoolId == schoolId)
            .GroupBy(item => item.Role)
            .Select(group => new MembershipCount(group.Key, group.Count(),
                group.Count(item => item.IsActive)))
            .ToListAsync(cancellationToken);
        return new SchoolDetails(
            school.Id, school.Name, school.Slug, school.Status, school.CreatedUtc, memberships);
    }

    /// <summary>Creates a school and its immutable audit event in one database save.</summary>
    public async Task<SchoolSummary> CreateSchoolAsync(
        string actorSubjectId,
        string name,
        string slug,
        CancellationToken cancellationToken)
    {
        var school = new School(name.Trim(), slug.Trim().ToLowerInvariant());
        dbContext.Schools.Add(school);
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            actorSubjectId, "school.created", "school", school.Id.ToString(), null));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SchoolSummary(school.Id, school.Name, school.Slug, school.Status,
            school.CreatedUtc, 0);
    }

    /// <summary>Changes a school's active status while preserving its records and memberships.</summary>
    public async Task<bool> SetSchoolStatusAsync(
        string actorSubjectId,
        Guid schoolId,
        SchoolStatus status,
        CancellationToken cancellationToken)
    {
        var school = await dbContext.Schools.SingleOrDefaultAsync(
            item => item.Id == schoolId, cancellationToken);
        if (school is null || school.Status == SchoolStatus.Archived ||
            status == SchoolStatus.Archived)
        {
            return false;
        }

        var oldStatus = school.Status;
        if (status == SchoolStatus.Suspended)
        {
            school.Suspend();
        }
        else
        {
            school.Reactivate();
        }

        if (oldStatus == school.Status)
        {
            return true;
        }

        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            actorSubjectId, "school.status.changed", "school", school.Id.ToString(),
            $"{oldStatus} -> {school.Status}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Lists known identities using memberships and demo credentials, not academic records.</summary>
    public async Task<PageResult<PlatformUserSummary>> GetUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var identities = dbContext.SchoolMemberships.Select(item => item.SubjectId)
            .Union(dbContext.PlatformAdministratorGrants.Select(item => item.SubjectId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var normalized = term.ToUpperInvariant();
            var matchingNames = dbContext.DemoCredentials
                .Where(item => item.NormalizedUserName.Contains(normalized))
                .Select(item => item.SubjectId);
            identities = identities.Where(subjectId =>
                subjectId.Contains(term) || matchingNames.Contains(subjectId));
        }

        var totalCount = await identities.Distinct().CountAsync(cancellationToken);
        var subjectIds = await identities.Distinct()
            .OrderBy(subjectId => subjectId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        if (subjectIds.Count == 0)
        {
            return new PageResult<PlatformUserSummary>([], totalCount, page, pageSize);
        }

        var credentials = await dbContext.DemoCredentials.AsNoTracking()
            .Where(item => subjectIds.Contains(item.SubjectId))
            .Select(item => new { item.SubjectId, item.UserName, item.IsActive })
            .ToDictionaryAsync(item => item.SubjectId, cancellationToken);
        var memberships = await dbContext.SchoolMemberships.AsNoTracking()
            .Where(item => subjectIds.Contains(item.SubjectId))
            .Join(dbContext.Schools.AsNoTracking(),
                membership => membership.SchoolId,
                school => school.Id,
                (membership, school) => new
                {
                    MembershipId = membership.Id,
                    membership.SubjectId,
                    SchoolId = school.Id,
                    SchoolName = school.Name,
                    membership.Role,
                    membership.IsActive
                })
            .ToListAsync(cancellationToken);
        var permissions = await dbContext.PlatformAdministratorGrants.AsNoTracking()
            .Where(item => subjectIds.Contains(item.SubjectId) &&
                           (item.Permission == PlatformPermissions.DashboardView ||
                            item.Permission == PlatformPermissions.SchoolsView ||
                            item.Permission == PlatformPermissions.SchoolsManage ||
                            item.Permission == PlatformPermissions.UsersView ||
                            item.Permission == PlatformPermissions.UsersManage ||
                            item.Permission == PlatformPermissions.RolesView ||
                            item.Permission == PlatformPermissions.RolesManage ||
                            item.Permission == PlatformPermissions.AuditView))
            .Select(item => new { item.SubjectId, item.Permission })
            .ToListAsync(cancellationToken);

        var items = subjectIds.Select(subjectId =>
        {
            credentials.TryGetValue(subjectId, out var credential);
            return new PlatformUserSummary(
                subjectId,
                credential?.UserName ?? subjectId,
                credential is null ? "Managed by identity provider" :
                credential.IsActive ? "Active (local demo)" : "Inactive (local demo)",
                memberships.Where(item => item.SubjectId == subjectId)
                .Select(item => new UserMembership(
                    item.MembershipId,
                    item.SchoolId, item.SchoolName, item.Role, item.IsActive))
                    .ToArray(),
                permissions.Where(item => item.SubjectId == subjectId)
                    .Select(item => item.Permission).Order().ToArray());
        }).ToArray();
        return new PageResult<PlatformUserSummary>(items, totalCount, page, pageSize);
    }

    /// <summary>Changes an existing school membership without changing platform permissions.</summary>
    public async Task<bool> UpdateMembershipAsync(
        string actorSubjectId,
        Guid membershipId,
        SchoolRole role,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.SchoolMemberships.SingleOrDefaultAsync(
            item => item.Id == membershipId, cancellationToken);
        if (membership is null || membership.SubjectId == actorSubjectId)
        {
            return false;
        }

        var oldRole = membership.Role;
        var oldActive = membership.IsActive;
        membership.ChangeRole(role);
        if (isActive)
        {
            membership.Activate();
        }
        else
        {
            membership.Deactivate();
        }

        if (oldRole != membership.Role || oldActive != membership.IsActive)
        {
            dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
                actorSubjectId, "membership.changed", "membership", membership.Id.ToString(),
                $"subject={membership.SubjectId}; {oldRole}/{oldActive} -> {membership.Role}/{membership.IsActive}"));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    /// <summary>Returns only explicitly delegable platform grants, never security or school-data access.</summary>
    public async Task<IReadOnlyList<PlatformGrantSummary>> GetGrantsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.PlatformAdministratorGrants.AsNoTracking()
            .Where(item =>
                item.Permission == PlatformPermissions.DashboardView ||
                item.Permission == PlatformPermissions.SchoolsView ||
                item.Permission == PlatformPermissions.SchoolsManage ||
                item.Permission == PlatformPermissions.UsersView ||
                item.Permission == PlatformPermissions.UsersManage ||
                item.Permission == PlatformPermissions.RolesView ||
                item.Permission == PlatformPermissions.RolesManage ||
                item.Permission == PlatformPermissions.AuditView)
            .OrderBy(item => item.SubjectId)
            .ThenBy(item => item.Permission)
            .Select(item => new PlatformGrantSummary(item.Id, item.SubjectId, item.Permission))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Assigns one allowlisted permission to a known identity and records the grant.</summary>
    public async Task<bool> GrantPermissionAsync(
        string actorSubjectId,
        string targetSubjectId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (actorSubjectId == targetSubjectId ||
            !PlatformPermissions.Delegable.Contains(permission) ||
            !await SubjectExistsAsync(targetSubjectId, cancellationToken))
        {
            return false;
        }

        if (await dbContext.PlatformAdministratorGrants.AnyAsync(
                grant => grant.SubjectId == targetSubjectId &&
                         grant.Permission == permission, cancellationToken))
        {
            return true;
        }

        dbContext.PlatformAdministratorGrants.Add(
            new PlatformAdministratorGrant(targetSubjectId, permission));
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            actorSubjectId, "platform.permission.granted", "identity", targetSubjectId,
            $"Capability granted: {permission}."));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Revokes one allowlisted delegated permission and records the change.</summary>
    public async Task<bool> RevokePermissionAsync(
        string actorSubjectId,
        Guid grantId,
        CancellationToken cancellationToken)
    {
        var grant = await dbContext.PlatformAdministratorGrants.SingleOrDefaultAsync(
            item => item.Id == grantId &&
                    (item.Permission == PlatformPermissions.DashboardView ||
                     item.Permission == PlatformPermissions.SchoolsView ||
                     item.Permission == PlatformPermissions.SchoolsManage ||
                     item.Permission == PlatformPermissions.UsersView ||
                     item.Permission == PlatformPermissions.UsersManage ||
                     item.Permission == PlatformPermissions.RolesView ||
                     item.Permission == PlatformPermissions.AuditView),
            cancellationToken);
        if (grant is null)
        {
            return false;
        }

        dbContext.PlatformAdministratorGrants.Remove(grant);
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
            actorSubjectId, "platform.permission.revoked", "identity", grant.SubjectId,
            $"Capability revoked: {grant.Permission}."));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Returns searchable administrative history without any authentication secrets.</summary>
    public async Task<PageResult<AuditSummary>> GetAuditEventsAsync(
        string? search,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<PlatformAuditEvent> query = dbContext.PlatformAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                item.ActorSubjectId.Contains(term) || item.Action.Contains(term) ||
                item.TargetType.Contains(term) || (item.TargetId != null && item.TargetId.Contains(term)));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.CreatedUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.CreatedUtc < toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditSummary(
                item.Id, item.ActorSubjectId, item.Action, item.TargetType,
                item.TargetId, item.Details, item.CreatedUtc))
            .ToListAsync(cancellationToken);
        return new PageResult<AuditSummary>(items, totalCount, page, pageSize);
    }

    /// <summary>Returns searchable authentication history created by EduCore-managed login flows.</summary>
    public async Task<PageResult<LoginSummary>> GetLoginEventsAsync(
        string? search,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<PlatformLoginEvent> query = dbContext.PlatformLoginEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                item.AccountIdentifier.Contains(term) || item.Outcome.Contains(term));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.CreatedUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.CreatedUtc < toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new LoginSummary(
                item.Id, item.AccountIdentifier, item.Succeeded, item.Outcome, item.CreatedUtc))
            .ToListAsync(cancellationToken);
        return new PageResult<LoginSummary>(items, totalCount, page, pageSize);
    }

    /// <summary>Checks the platform directory, without revealing whether an external identity is valid.</summary>
    private async Task<bool> SubjectExistsAsync(string subjectId, CancellationToken cancellationToken)
    {
        return await dbContext.SchoolMemberships.AnyAsync(
                   item => item.SubjectId == subjectId, cancellationToken) ||
               await dbContext.PlatformAdministratorGrants.AnyAsync(
                   item => item.SubjectId == subjectId, cancellationToken) ||
               await dbContext.DemoCredentials.AnyAsync(
                   item => item.SubjectId == subjectId, cancellationToken);
    }

    public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
    public sealed record PlatformDashboard(
        int SchoolCount,
        int ActiveSchoolCount,
        int SuspendedSchoolCount,
        int KnownIdentityCount,
        int ActiveMembershipIdentityCount,
        int SuccessfulLoginsLastDay,
        int FailedLoginsLastDay,
        int AdministrativeChangesLastWeek,
        bool DatabaseConnected);
    public sealed record SchoolSummary(
        Guid Id, string Name, string Slug, SchoolStatus Status,
        DateTimeOffset CreatedUtc, int MembershipCount);
    public sealed record MembershipCount(SchoolRole Role, int Total, int Active);
    public sealed record SchoolDetails(
        Guid Id, string Name, string Slug, SchoolStatus Status,
        DateTimeOffset CreatedUtc, IReadOnlyList<MembershipCount> MembershipCounts);
    public sealed record UserMembership(
        Guid Id, Guid SchoolId, string SchoolName, SchoolRole Role, bool IsActive);
    public sealed record PlatformUserSummary(
        string SubjectId, string LoginIdentifier, string AccountStatus,
        IReadOnlyList<UserMembership> Memberships, IReadOnlyList<string> Permissions);
    public sealed record PlatformGrantSummary(Guid Id, string SubjectId, string Permission);
    public sealed record AuditSummary(
        Guid Id, string ActorSubjectId, string Action, string TargetType,
        string? TargetId, string? Details, DateTimeOffset CreatedUtc);
    public sealed record LoginSummary(
        Guid Id, string AccountIdentifier, bool Succeeded, string Outcome,
        DateTimeOffset CreatedUtc);
}
