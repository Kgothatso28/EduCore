using System.Security.Claims;
using EduCore.Api.Data;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using EduCore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EduCore.Api.Tests;

/// <summary>Verifies tenant isolation, authorization decisions, and repeatable demo seeding.</summary>
public sealed class TenantIsolationTests : IDisposable
{
    private readonly string _databaseName = $"EduCoreTenantTests_{Guid.NewGuid():N}";
    private readonly Guid _schoolA;
    private readonly Guid _schoolB;
    private const string UserId = "identity-provider|user-1";
    private const string StudentUserId = "identity-provider|student-1";
    private string ConnectionString =>
        $@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database={_databaseName};TrustServerCertificate=true";

    /// <summary>Creates two school tenants and membership/student fixtures in an isolated database.</summary>
    public TenantIsolationTests()
    {
        using var db = CreateDbContext(new TenantContext());
        db.Database.Migrate();
        var schoolA = new School("School A", "school-a");
        var schoolB = new School("School B", "school-b");
        _schoolA = schoolA.Id;
        _schoolB = schoolB.Id;
        db.Schools.AddRange(schoolA, schoolB);
        db.SchoolMemberships.AddRange(
            new SchoolMembership(_schoolA, UserId, SchoolRole.SchoolAdministrator),
            new SchoolMembership(_schoolB, UserId, SchoolRole.Teacher),
            new SchoolMembership(_schoolA, StudentUserId, SchoolRole.Student));
        db.SaveChanges();

        SeedStudent(_schoolA, "A-001", "Ava", "One");
        SeedStudent(_schoolB, "B-001", "Ben", "Two");
    }

    /// <summary>Ensures school-scoped reads and ID lookups conceal records owned by another school.</summary>
    [Fact]
    public async Task Active_membership_scopes_read_and_hides_foreign_record_ids()
    {
        var tenant = await ResolveTenant(_schoolA);
        using var db = CreateDbContext(tenant);
        var service = new StudentService(db, tenant);
        var students = await service.ListAsync(default);
        var foreign = await db.Students.IgnoreQueryFilters()
            .SingleAsync(student => student.SchoolId == _schoolB);

        Assert.Single(students);
        Assert.Equal("A-001", students[0].LocalId);
        Assert.Null(await service.FindAsync(foreign.Id, default));
        Assert.False(await service.UpdateAsync(foreign.Id, "HACK", "Eve", "Other", default));
        Assert.False(await service.DeleteAsync(foreign.Id, default));
        Assert.Equal("B-001", foreign.LocalId);
    }

    /// <summary>Ensures create authority comes from validated context rather than a client-supplied school ID.</summary>
    [Fact]
    public async Task School_id_is_never_accepted_as_create_authority()
    {
        var tenant = await ResolveTenant(_schoolA);
        using var db = CreateDbContext(tenant);
        var service = new StudentService(db, tenant);

        var created = await service.CreateAsync("A-002", "Casey", "Three", default);

        Assert.Equal(_schoolA, created.SchoolId);
        Assert.DoesNotContain(
            typeof(EduCore.Api.Endpoints.StudentEndpoints.StudentWriteRequest)
                .GetProperties(),
            property => property.Name == nameof(Student.SchoolId));
    }

    /// <summary>Confirms an absent tenant fails closed for reads and writes.</summary>
    [Fact]
    public async Task No_tenant_context_reads_nothing_and_rejects_writes()
    {
        var tenant = new TenantContext();
        using var db = CreateDbContext(tenant);

        Assert.Empty(await db.Students.ToListAsync());
        db.Students.Add(new Student(_schoolA, "A-002", "Casey", "Three"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    /// <summary>Rejects tenant establishment when the requested school is suspended.</summary>
    [Fact]
    public async Task Membership_must_be_active_and_school_must_not_be_suspended()
    {
        var tenant = new TenantContext();
        using var db = CreateDbContext(tenant);
        var school = await db.Schools.SingleAsync(item => item.Id == _schoolB);
        school.Suspend();
        await db.SaveChangesAsync();

        var resolver = new SchoolContextResolver(db, tenant);
        Assert.False(await resolver.TryEstablishAsync(Principal(_schoolB)));
        Assert.False(tenant.IsValidated);
    }

    /// <summary>Rejects tenant establishment when the subject's school membership is inactive.</summary>
    [Fact]
    public async Task Inactive_membership_cannot_establish_school_context()
    {
        using (var seedDb = CreateDbContext(new TenantContext()))
        {
            var membership = await seedDb.SchoolMemberships
                .SingleAsync(item => item.SchoolId == _schoolB);
            membership.Deactivate();
            await seedDb.SaveChangesAsync();
        }

        var tenant = new TenantContext();
        using var db = CreateDbContext(tenant);
        var resolver = new SchoolContextResolver(db, tenant);

        Assert.False(await resolver.TryEstablishAsync(Principal(_schoolB)));
        Assert.False(tenant.IsValidated);
    }

    /// <summary>Allows authentication recovery from stale claims but blocks protected tenant requests.</summary>
    [Fact]
    public async Task Stale_school_claim_allows_auth_recovery_but_blocks_tenant_routes()
    {
        using var db = CreateDbContext(new TenantContext());
        var resolver = new SchoolContextResolver(db, new TenantContext());
        var principal = Principal(Guid.NewGuid());
        var middleware = new SchoolTenantMiddleware(_ => Task.CompletedTask);
        var authContext = new DefaultHttpContext
        {
            User = principal
        };
        authContext.Request.Path = "/api/v1/auth/logout";

        await middleware.InvokeAsync(authContext, resolver);

        var tenantContext = new DefaultHttpContext
        {
            User = principal
        };
        tenantContext.Request.Path = "/api/v1/students";

        await middleware.InvokeAsync(tenantContext, resolver);

        Assert.Equal(StatusCodes.Status200OK, authContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, tenantContext.Response.StatusCode);
    }

    /// <summary>Allows an identity to resolve each valid membership but rejects an unrelated subject.</summary>
    [Fact]
    public async Task Multi_school_user_can_resolve_only_a_valid_membership()
    {
        var tenantA = await ResolveTenant(_schoolA);
        var tenantB = await ResolveTenant(_schoolB);

        Assert.Equal(_schoolA, tenantA.SchoolId);
        Assert.Equal(_schoolB, tenantB.SchoolId);

        var outsider = new TenantContext();
        using var db = CreateDbContext(outsider);
        var resolver = new SchoolContextResolver(db, outsider);
        Assert.False(await resolver.TryEstablishAsync(Principal(Guid.NewGuid())));
        Assert.False(outsider.IsValidated);
    }

    /// <summary>Enforces student identifier uniqueness within a school without colliding across schools.</summary>
    [Fact]
    public async Task Student_identifier_is_unique_per_school_but_can_repeat_across_schools()
    {
        var tenantA = await ResolveTenant(_schoolA);
        using (var db = CreateDbContext(tenantA))
        {
            db.Students.Add(new Student(_schoolA, "A-001", "Duplicate", "A"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        var tenantB = await ResolveTenant(_schoolB);
        using var dbB = CreateDbContext(tenantB);
        dbB.Students.Add(new Student(_schoolB, "A-001", "Same Local ID", "B"));
        await dbB.SaveChangesAsync();
    }

    /// <summary>Prevents a persisted tenant-owned record from being reassigned to another school.</summary>
    [Fact]
    public async Task Tenant_id_cannot_be_changed_after_record_creation()
    {
        var tenantA = await ResolveTenant(_schoolA);
        using var db = CreateDbContext(tenantA);
        var student = await db.Students.SingleAsync();
        typeof(TenantOwnedEntity).GetProperty(nameof(TenantOwnedEntity.SchoolId))!
            .SetValue(student, _schoolB);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    /// <summary>Verifies seeding produces two isolated tenants and is safe to repeat.</summary>
    [Fact]
    public async Task Demo_seed_is_two_tenant_persisted_and_repeatable()
    {
        var databaseName = $"EduCore_Demo_Test_{Guid.NewGuid():N}";
        var connectionString =
            $@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database={databaseName};TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<EduCoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:EduCore"] = connectionString,
                ["DemoAuth:NorthAdmin"] = "north-admin",
                ["DemoAuth:SouthAdmin"] = "south-admin",
                ["DemoAuth:NorthPassword"] = "North-Test-Password-123!",
                ["DemoAuth:SouthPassword"] = "South-Test-Password-456!",
                ["DemoAuth:NorthTeacher"] = "north-teacher",
                ["DemoAuth:NorthTeacherPassword"] = "Teacher-Test-Password-789!",
                ["DemoAuth:NorthStudent"] = "north-student",
                ["DemoAuth:NorthStudentPassword"] = "Student-Test-Password-123!"
            }).Build();
        try
        {
            var hasher = new PasswordHasher<DemoCredential>();
            var seeder = new DemoSeeder(options, hasher, config);
            await seeder.SeedAsync(default);
            await seeder.SeedAsync(default);

            using var db = new EduCoreDbContext(options, new TenantContext());
            Assert.Equal(2, await db.Schools.CountAsync());
            Assert.Equal(4, await db.DemoCredentials.CountAsync());
            Assert.Equal(6, await db.Students.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await db.Classes.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await db.Teachers.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await db.TeachingAssignments.IgnoreQueryFilters().CountAsync());
            Assert.All(await db.DemoCredentials.ToListAsync(), credential =>
                Assert.False(credential.PasswordHash.Contains("Password", StringComparison.Ordinal)));
        }
        finally
        {
            using var cleanup = new EduCoreDbContext(options, new TenantContext());
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>Checks school listing order and SQL translation of the active membership query.</summary>
    [Fact]
    public async Task Demo_school_list_is_sorted_and_translatable_by_sql_server()
    {
        using var db = CreateDbContext(new TenantContext());
        var service = new DemoAuthenticationService(
            db, new PasswordHasher<DemoCredential>());

        var schools = await service.GetSchoolsAsync(UserId, default);

        Assert.Equal(["School A", "School B"], schools.Select(school => school.Name));
        Assert.Equal(
            [SchoolRole.SchoolAdministrator, SchoolRole.Teacher],
            schools.Select(school => school.Role));
    }

    /// <summary>Restricts teacher rosters to assigned classes and student views to linked identities.</summary>
    [Fact]
    public async Task Role_dashboards_return_only_assigned_or_linked_school_records()
    {
        var teacherTenant = await ResolveTenant(_schoolB);
        Guid classId;
        using (var db = CreateDbContext(teacherTenant))
        {
            var schoolClass = new SchoolClass(_schoolB, "Grade 7 Green", "Grade 7");
            var teacher = new Teacher(_schoolB, UserId, "Jamie", "Teacher");
            db.AddRange(schoolClass, teacher);
            await db.SaveChangesAsync();
            classId = schoolClass.Id;
            db.TeachingAssignments.Add(new TeachingAssignment(
                _schoolB, teacher.Id, schoolClass.Id, "Science"));
            db.TeachingAssignments.Add(new TeachingAssignment(
                _schoolB, teacher.Id, schoolClass.Id, "Mathematics"));
            db.Students.Add(new Student(
                _schoolB, "B-002", "Riley", "Student", schoolClass.Id, "student|riley"));
            await db.SaveChangesAsync();
        }

        var principalTenant = await ResolveTenant(_schoolA);
        Guid foreignClassId;
        using (var db = CreateDbContext(principalTenant))
        {
            var foreignClass = new SchoolClass(_schoolA, "Grade 7 Green", "Grade 7");
            db.Classes.Add(foreignClass);
            await db.SaveChangesAsync();
            foreignClassId = foreignClass.Id;
        }

        using (var db = CreateDbContext(teacherTenant))
        {
            var dashboards = new DashboardService(db, teacherTenant);
            var teacherDashboard = await dashboards.GetTeacherAsync(UserId, default);
            Assert.NotNull(teacherDashboard);
            Assert.Equal(2, teacherDashboard.Classes.Count);
            Assert.Contains(teacherDashboard.Classes, item => item.SubjectName == "Science");
            Assert.All(teacherDashboard.Classes, item => Assert.Equal(1, item.StudentCount));
            Assert.Equal(1, teacherDashboard.AssignedClassCount);
            Assert.Equal(2, teacherDashboard.AssignedSubjectCount);
            Assert.Equal(1, teacherDashboard.AssignedStudentCount);
            Assert.Single(teacherDashboard.StudentsByClass);

            var roster = await dashboards.GetTeacherRosterAsync(UserId, classId, default);
            Assert.NotNull(roster);
            Assert.Single(roster);
            Assert.Equal("B-002", roster[0].LocalId);
            Assert.Null(await dashboards.GetTeacherRosterAsync(UserId, Guid.NewGuid(), default));
            Assert.Null(await dashboards.GetTeacherRosterAsync(UserId, foreignClassId, default));

            var student = await dashboards.GetStudentAsync("student|riley", default);
            Assert.NotNull(student);
            Assert.Equal("Grade 7 Green", student.ClassName);
            Assert.Equal(["Mathematics", "Science"], student.Subjects);
            Assert.Null(await dashboards.GetStudentAsync("student|other", default));
        }
    }

    /// <summary>Aggregates principal charts from the active tenant and validates selected years.</summary>
    [Fact]
    public async Task Principal_analytics_are_sql_aggregated_tenant_scoped_and_year_filtered()
    {
        var tenant = await ResolveTenant(_schoolA);
        using var db = CreateDbContext(tenant);
        var schoolClass = new SchoolClass(_schoolA, "Grade 4 Gold", "Grade 4");
        db.Classes.Add(schoolClass);
        await db.SaveChangesAsync();
        db.Students.Add(new Student(_schoolA, "A-002", "Sam", "Classmate", schoolClass.Id));
        await db.SaveChangesAsync();

        var analytics = new DashboardService(db, tenant);
        var currentYear = DateTime.UtcNow.Year;
        var current = await analytics.GetPrincipalAnalyticsAsync(currentYear, default);

        Assert.NotNull(current);
        Assert.Equal(2, current.CurrentStudentCount);
        Assert.Equal(1, current.ClassCount);
        Assert.Equal(12, current.MonthlyEnrollments.Count);
        Assert.Equal(2, current.MonthlyEnrollments.Sum(item => item.Count));
        Assert.Contains(current.StudentsByClass, item =>
            item.Label == "Grade 4 · Grade 4 Gold" && item.Count == 1);
        Assert.Contains(current.StudentsByClass, item =>
            item.Label == "Unassigned" && item.Count == 1);
        Assert.DoesNotContain(current.StudentsByClass, item => item.Label.Contains("School B"));

        var priorYear = await analytics.GetPrincipalAnalyticsAsync(currentYear - 1, default);
        Assert.NotNull(priorYear);
        Assert.Equal(2, priorYear.CurrentStudentCount);
        Assert.All(priorYear.MonthlyEnrollments, item => Assert.Equal(0, item.Count));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            analytics.GetPrincipalAnalyticsAsync(currentYear + 1, default));
    }

    /// <summary>Exercises platform metadata queries while keeping tenant academic records out of the service.</summary>
    [Fact]
    public async Task Platform_administration_lists_only_school_and_identity_metadata()
    {
        using var db = CreateDbContext(new TenantContext());
        db.PlatformAdministratorGrants.Add(new PlatformAdministratorGrant(
            UserId, EduCore.Api.Security.PlatformPermissions.SchoolsView));
        db.PlatformLoginEvents.Add(new PlatformLoginEvent(
            "north-admin", true, "success"));
        db.PlatformLoginEvents.Add(new PlatformLoginEvent(
            "north-admin", true, "logout"));
        db.PlatformLoginEvents.Add(new PlatformLoginEvent(
            "unknown-user", false, "invalid_credentials"));
        await db.SaveChangesAsync();

        var administration = new PlatformAdministrationService(db);
        var dashboard = await administration.GetDashboardAsync(default);
        Assert.Equal(2, dashboard.SchoolCount);
        Assert.Equal(2, dashboard.ActiveSchoolCount);
        Assert.Equal(2, dashboard.KnownIdentityCount);
        Assert.Equal(2, dashboard.ActiveMembershipIdentityCount);
        Assert.Equal(1, dashboard.SuccessfulLoginsLastDay);
        Assert.Equal(1, dashboard.FailedLoginsLastDay);

        var schools = await administration.GetSchoolsAsync(
            "school-a", null, "name", false, 1, 20, default);
        Assert.Single(schools.Items);
        Assert.Equal("school-a", schools.Items[0].Slug);

        var details = await administration.GetSchoolAsync(_schoolA, default);
        Assert.NotNull(details);
        Assert.Contains(details.MembershipCounts, item =>
            item.Role == SchoolRole.SchoolAdministrator && item.Active == 1);
        Assert.Contains(details.Administrators, item =>
            item.SubjectId == UserId && item.IsActive);

        var users = await administration.GetUsersAsync("user-1", 1, 20, default);
        Assert.Single(users.Items);
        Assert.Equal(UserId, users.Items[0].SubjectId);
        Assert.Equal(2, users.Items[0].Memberships.Count);
        Assert.DoesNotContain(users.Items[0].Memberships, item =>
            item.Role == SchoolRole.Student);
        Assert.Contains(await administration.GetGrantsAsync(default), item =>
            item.SubjectId == UserId &&
            item.Permission == EduCore.Api.Security.PlatformPermissions.SchoolsView);

        var logins = await administration.GetLoginEventsAsync(
            "unknown-user", null, null, 1, 20, default);
        Assert.Single(logins.Items);
        Assert.Equal("invalid_credentials", logins.Items[0].Outcome);
    }

    /// <summary>Creates and audits a school role while preserving backend membership enforcement.</summary>
    [Fact]
    public async Task Platform_membership_assignment_is_audited_and_revocation_blocks_access()
    {
        using var db = CreateDbContext(new TenantContext());
        var service = new PlatformAdministrationService(db);

        var created = await service.CreateMembershipAsync(
            "platform-root", _schoolA, "new-teacher", SchoolRole.Teacher, default);
        Assert.Equal(
            PlatformAdministrationService.MembershipCreateStatus.Created,
            created.Status);
        Assert.NotNull(created.MembershipId);
        Assert.Equal(
            PlatformAdministrationService.MembershipCreateStatus.AlreadyExists,
            (await service.CreateMembershipAsync(
                "platform-root", _schoolA, "new-teacher", SchoolRole.Teacher, default)).Status);
        Assert.Equal(
            PlatformAdministrationService.MembershipCreateStatus.SelfAssignment,
            (await service.CreateMembershipAsync(
                "platform-root", _schoolA, "platform-root", SchoolRole.SchoolAdministrator, default)).Status);

        var membership = await service.GetMembershipAsync(created.MembershipId!.Value, default);
        Assert.NotNull(membership);
        Assert.Equal("new-teacher", membership.SubjectId);
        Assert.Equal(SchoolRole.Teacher, membership.Role);
        Assert.True(await service.UpdateMembershipAsync(
            "platform-root", membership.Id, SchoolRole.Teacher, false, default));

        using var verificationDb = CreateDbContext(new TenantContext());
        var resolver = new SchoolContextResolver(verificationDb, new TenantContext());
        Assert.False(await resolver.TryEstablishAsync(Principal(_schoolA, "new-teacher")));
        Assert.Contains((await service.GetAuditEventsAsync(
            "membership.", null, null, 1, 20, default)).Items, item =>
            item.Action == "membership.created");
        Assert.Contains((await service.GetAuditEventsAsync(
            "membership.", null, null, 1, 20, default)).Items, item =>
            item.Action == "membership.changed");
    }

    /// <summary>Requires explicit database grants and never infers sensitive school-data access.</summary>
    [Fact]
    public async Task Platform_capability_requires_an_explicit_grant_and_denies_sensitive_data()
    {
        using var db = CreateDbContext(new TenantContext());
        db.PlatformAdministratorGrants.Add(new PlatformAdministratorGrant(
            UserId, EduCore.Api.Security.PlatformPermissions.SchoolsView));
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, UserId)], "test"));
        var handler = new PlatformPermissionHandler(db);
        var platformRequirement = new PlatformPermissionRequirement(
            EduCore.Api.Security.PlatformPermissions.SchoolsView);
        var platformContext = new AuthorizationHandlerContext(
            [platformRequirement], user, resource: null);
        await handler.HandleAsync(platformContext);
        Assert.True(platformContext.HasSucceeded);

        var sensitiveRequirement = new PlatformPermissionRequirement("schooldata.academic.view");
        var sensitiveContext = new AuthorizationHandlerContext(
            [sensitiveRequirement], user, resource: null);
        await handler.HandleAsync(sensitiveContext);
        Assert.False(sensitiveContext.HasSucceeded);

        var rolesManageRequirement = new PlatformPermissionRequirement(
            EduCore.Api.Security.PlatformPermissions.RolesManage);
        var rolesContext = new AuthorizationHandlerContext(
            [rolesManageRequirement], user, resource: null);
        await handler.HandleAsync(rolesContext);
        Assert.False(rolesContext.HasSucceeded);

        var identityManageRequirement = new PlatformPermissionRequirement(
            EduCore.Api.Security.PlatformPermissions.IdentityManage);
        var identityContext = new AuthorizationHandlerContext(
            [identityManageRequirement], user, resource: null);
        await handler.HandleAsync(identityContext);
        Assert.False(identityContext.HasSucceeded);
        Assert.DoesNotContain(
            EduCore.Api.Security.PlatformPermissions.IdentityManage,
            EduCore.Api.Security.PlatformPermissions.Delegable);
        Assert.Contains(
            EduCore.Api.Security.PlatformPermissions.IdentityManage,
            EduCore.Api.Security.PlatformPermissions.Bootstrap);
    }

    /// <summary>Prevents ordinary grants from escalating, self-assigning, or enabling school-data access.</summary>
    [Fact]
    public async Task Platform_permission_delegation_is_allowlisted_and_self_grants_are_rejected()
    {
        using var db = CreateDbContext(new TenantContext());
        db.PlatformAdministratorGrants.Add(new PlatformAdministratorGrant(
            "platform-root", EduCore.Api.Security.PlatformPermissions.RolesManage));
        await db.SaveChangesAsync();
        var service = new PlatformAdministrationService(db);

        Assert.False(await service.GrantPermissionAsync(
            "platform-root", "platform-root",
            EduCore.Api.Security.PlatformPermissions.SchoolsView, default));
        Assert.False(await service.GrantPermissionAsync(
            "platform-root", UserId,
            EduCore.Api.Security.PlatformPermissions.RolesManage, default));
        Assert.False(await service.GrantPermissionAsync(
            "platform-root", UserId, "schooldata.academic.view", default));
        Assert.False(await service.GrantPermissionAsync(
            "platform-root", UserId,
            EduCore.Api.Security.PlatformPermissions.IdentityManage, default));
        Assert.True(await service.GrantPermissionAsync(
            "platform-root", UserId,
            EduCore.Api.Security.PlatformPermissions.SchoolsView, default));

        var events = await service.GetAuditEventsAsync(
            "platform.permission.granted", null, null, 1, 20, default);
        Assert.Single(events.Items);
    }

    /// <summary>Bootstraps one platform role manager from trusted configuration and blocks repetition.</summary>
    [Fact]
    public async Task Platform_bootstrap_is_explicit_and_one_time()
    {
        using var db = CreateDbContext(new TenantContext());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlatformAdmin:BootstrapSubjectId"] = "platform-root"
            })
            .Build();
        var bootstrapper = new PlatformAdminBootstrapper(db, configuration);

        await bootstrapper.ProvisionFirstAdministratorAsync(default);
        var grants = await db.PlatformAdministratorGrants
            .Where(item => item.SubjectId == "platform-root")
            .ToListAsync();
        Assert.Contains(grants, item =>
            item.Permission == EduCore.Api.Security.PlatformPermissions.RolesManage);
        Assert.DoesNotContain(grants, item =>
            item.Permission.StartsWith("schooldata.", StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrapper.ProvisionFirstAdministratorAsync(default));
    }

    /// <summary>Only an existing platform security administrator can receive the new identity capability.</summary>
    [Fact]
    public async Task Identity_management_capability_upgrade_requires_existing_security_admin()
    {
        using var db = CreateDbContext(new TenantContext());
        db.PlatformAdministratorGrants.Add(new PlatformAdministratorGrant(
            "platform-root", EduCore.Api.Security.PlatformPermissions.RolesManage));
        await db.SaveChangesAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlatformAdmin:BootstrapSubjectId"] = "platform-root"
            })
            .Build();
        var bootstrapper = new PlatformAdminBootstrapper(db, configuration);

        await bootstrapper.ProvisionIdentityManagementCapabilityAsync(default);
        Assert.True(await db.PlatformAdministratorGrants.AnyAsync(item =>
            item.SubjectId == "platform-root" &&
            item.Permission == EduCore.Api.Security.PlatformPermissions.IdentityManage));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrapper.ProvisionIdentityManagementCapabilityAsync(default));
        Assert.Single(await db.PlatformAuditEvents.Where(item =>
            item.Action == "platform.identity.manage.provisioned").ToListAsync());
    }

    /// <summary>Audits school creation and status changes while refusing changes to archived schools.</summary>
    [Fact]
    public async Task Platform_school_lifecycle_preserves_records_and_audits_changes()
    {
        using var db = CreateDbContext(new TenantContext());
        var administration = new PlatformAdministrationService(db);
        var created = await administration.CreateSchoolAsync(
            "platform-root", "New School", "new-school", default);

        Assert.True(await administration.SetSchoolStatusAsync(
            "platform-root", created.Id, SchoolStatus.Suspended, default));
        Assert.True(await administration.SetSchoolStatusAsync(
            "platform-root", created.Id, SchoolStatus.Active, default));

        var school = await db.Schools.SingleAsync(item => item.Id == created.Id);
        school.Archive();
        await db.SaveChangesAsync();
        Assert.False(await administration.SetSchoolStatusAsync(
            "platform-root", created.Id, SchoolStatus.Active, default));
        Assert.Equal(3, await db.PlatformAuditEvents.CountAsync());
    }

    /// <summary>Protects recorded administrative history from edits and deletion through EF writes.</summary>
    [Fact]
    public async Task Platform_audit_records_are_append_only()
    {
        using var db = CreateDbContext(new TenantContext());
        var audit = new PlatformAuditEvent(
            UserId, "school.created", "school", _schoolA.ToString(), null);
        db.PlatformAuditEvents.Add(audit);
        await db.SaveChangesAsync();

        db.Entry(audit).Property(item => item.Action).CurrentValue = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    /// <summary>Rejects new class records when the request has no validated matching tenant.</summary>
    [Fact]
    public async Task New_role_records_require_the_validated_school_context()
    {
        using var db = CreateDbContext(new TenantContext());
        db.Classes.Add(new SchoolClass(_schoolA, "Unscoped Class", "Grade 1"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    /// <summary>Keeps platform administration privileges distinct from school-scoped roles.</summary>
    [Fact]
    public void School_roles_do_not_include_platform_administrator()
    {
        Assert.DoesNotContain(
            Enum.GetNames<SchoolRole>(),
            role => role.Contains("Platform", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(typeof(SchoolMembership), typeof(PlatformAdministratorGrant));
    }

    /// <summary>Ensures a teacher membership cannot satisfy an administrator-only requirement.</summary>
    [Fact]
    public async Task Teacher_membership_does_not_authorize_school_administrator_operations()
    {
        var teacherContext = await ResolveTenant(_schoolB);
        var requirement = new ActiveSchoolMembershipRequirement(SchoolRole.SchoolAdministrator);
        var authorizationContext = new AuthorizationHandlerContext(
            [requirement], Principal(_schoolB), resource: null);

        await new ActiveSchoolMembershipHandler(teacherContext)
            .HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    /// <summary>Ensures an administrator membership satisfies the matching school role requirement.</summary>
    [Fact]
    public async Task School_administrator_membership_authorizes_school_administrator_operations()
    {
        var adminContext = await ResolveTenant(_schoolA);
        var requirement = new ActiveSchoolMembershipRequirement(SchoolRole.SchoolAdministrator);
        var authorizationContext = new AuthorizationHandlerContext(
            [requirement], Principal(_schoolA), resource: null);

        await new ActiveSchoolMembershipHandler(adminContext)
            .HandleAsync(authorizationContext);

        Assert.True(authorizationContext.HasSucceeded);
    }

    /// <summary>Allows teacher and student policies only for their exact active-school roles.</summary>
    [Fact]
    public async Task Teacher_and_student_memberships_authorize_only_matching_role_policies()
    {
        var teacherTenant = await ResolveTenant(_schoolB);
        var teacherRequirement = new ActiveSchoolMembershipRequirement(SchoolRole.Teacher);
        var teacherAuthorization = new AuthorizationHandlerContext(
            [teacherRequirement], Principal(_schoolB), resource: null);
        await new ActiveSchoolMembershipHandler(teacherTenant).HandleAsync(teacherAuthorization);
        Assert.True(teacherAuthorization.HasSucceeded);

        var studentTenant = await ResolveTenant(_schoolA, StudentUserId);
        var studentRequirement = new ActiveSchoolMembershipRequirement(SchoolRole.Student);
        var studentAuthorization = new AuthorizationHandlerContext(
            [studentRequirement], Principal(_schoolA, StudentUserId), resource: null);
        await new ActiveSchoolMembershipHandler(studentTenant).HandleAsync(studentAuthorization);
        Assert.True(studentAuthorization.HasSucceeded);
    }

    /// <summary>Builds a tenant context only after the resolver validates the supplied school claim.</summary>
    private async Task<TenantContext> ResolveTenant(Guid schoolId, string subjectId = UserId)
    {
        var tenant = new TenantContext();
        using var db = CreateDbContext(tenant);
        var resolver = new SchoolContextResolver(db, tenant);
        Assert.True(await resolver.TryEstablishAsync(Principal(schoolId, subjectId)));
        return tenant;
    }

    /// <summary>Persists one fixture student after resolving its owning school for the test identity.</summary>
    private void SeedStudent(Guid schoolId, string localId, string firstName, string lastName)
    {
        var tenant = new TenantContext();
        using var db = CreateDbContext(tenant);
        var resolver = new SchoolContextResolver(db, tenant);
        Assert.True(resolver.TryEstablishAsync(Principal(schoolId)).GetAwaiter().GetResult());
        db.Students.Add(new Student(schoolId, localId, firstName, lastName));
        db.SaveChanges();
    }

    /// <summary>Creates a SQL-backed context using the test database and supplied request tenant.</summary>
    private EduCoreDbContext CreateDbContext(TenantContext tenant) =>
        new(new DbContextOptionsBuilder<EduCoreDbContext>()
            .UseSqlServer(ConnectionString)
            .Options, tenant);

    /// <summary>Creates a test identity carrying a school claim for resolver and authorization checks.</summary>
    private static ClaimsPrincipal Principal(Guid schoolId, string subjectId = UserId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, subjectId),
            new Claim(SchoolContextResolver.ActiveSchoolClaim, schoolId.ToString())
        ], authenticationType: "test"));

    /// <summary>Deletes the test database so fixtures cannot leak between test cases.</summary>
    public void Dispose()
    {
        using var db = CreateDbContext(new TenantContext());
        db.Database.EnsureDeleted();
    }
}
