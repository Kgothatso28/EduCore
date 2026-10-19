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
            new SchoolMembership(_schoolB, UserId, SchoolRole.Teacher));
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
                ["DemoAuth:SouthPassword"] = "South-Test-Password-456!"
            }).Build();
        try
        {
            var hasher = new PasswordHasher<DemoCredential>();
            var seeder = new DemoSeeder(options, hasher, config);
            await seeder.SeedAsync(default);
            await seeder.SeedAsync(default);

            using var db = new EduCoreDbContext(options, new TenantContext());
            Assert.Equal(2, await db.Schools.CountAsync());
            Assert.Equal(2, await db.DemoCredentials.CountAsync());
            Assert.Equal(6, await db.Students.IgnoreQueryFilters().CountAsync());
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

    /// <summary>Builds a tenant context only after the resolver validates the supplied school claim.</summary>
    private async Task<TenantContext> ResolveTenant(Guid schoolId)
    {
        var tenant = new TenantContext();
        using var db = CreateDbContext(tenant);
        var resolver = new SchoolContextResolver(db, tenant);
        Assert.True(await resolver.TryEstablishAsync(Principal(schoolId)));
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
    private static ClaimsPrincipal Principal(Guid schoolId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(SchoolContextResolver.ActiveSchoolClaim, schoolId.ToString())
        ], authenticationType: "test"));

    /// <summary>Deletes the test database so fixtures cannot leak between test cases.</summary>
    public void Dispose()
    {
        using var db = CreateDbContext(new TenantContext());
        db.Database.EnsureDeleted();
    }
}
