using EduCore.Api.Data;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Services;

/// <summary>Creates repeatable local demo tenants and sample accounts in a guarded demo database.</summary>
public sealed class DemoSeeder(
    DbContextOptions<EduCoreDbContext> dbOptions,
    IPasswordHasher<DemoCredential> passwordHasher,
    IConfiguration configuration)
{
    /// <summary>Seeds only the optional platform demo identity and applies additive migrations.</summary>
    public async Task SeedPlatformAdminAsync(CancellationToken cancellationToken)
    {
        EnsureDemoDatabase();
        using var dbContext = new EduCoreDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync(cancellationToken);
        await CreatePlatformAdminAsync(dbContext, cancellationToken);
    }

    /// <summary>Applies migrations, provisions two school admins, and seeds isolated sample students.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        EnsureDemoDatabase();

        School north;
        School south;
        using (var dbContext = new EduCoreDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            north = await GetOrCreateSchoolAsync(dbContext, "Northfield Academy", "northfield",
                cancellationToken);
            south = await GetOrCreateSchoolAsync(dbContext, "Southridge School", "southridge",
                cancellationToken);
            await CreateAdminAsync(dbContext, north, "DemoAuth:NorthAdmin", "DemoAuth:NorthPassword",
                "north-admin", cancellationToken);
            await CreateAdminAsync(dbContext, south, "DemoAuth:SouthAdmin", "DemoAuth:SouthPassword",
                "south-admin", cancellationToken);
            await CreateRoleAccountAsync(dbContext, north, "DemoAuth:NorthTeacher",
                "DemoAuth:NorthTeacherPassword", "north-teacher", SchoolRole.Teacher,
                cancellationToken);
            await CreateRoleAccountAsync(dbContext, north, "DemoAuth:NorthStudent",
                "DemoAuth:NorthStudentPassword", "north-student", SchoolRole.Student,
                cancellationToken);
            await CreatePlatformAdminAsync(dbContext, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await SeedStudentsAsync(north, "N", cancellationToken);
        await SeedStudentsAsync(south, "S", cancellationToken);
        await SeedNorthfieldLearningAsync(north, cancellationToken);
    }

    /// <summary>Restricts all local demo seed operations to a distinctly named demo database.</summary>
    private void EnsureDemoDatabase()
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            configuration.GetConnectionString("EduCore"));
        if (!connection.InitialCatalog.StartsWith("EduCore_Demo_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Demo seeding is restricted to a database named EduCore_Demo_*.");
        }
    }

    /// <summary>Returns an existing school by slug or persists a new one.</summary>
    private async Task<School> GetOrCreateSchoolAsync(
        EduCoreDbContext dbContext, string name, string slug,
        CancellationToken cancellationToken)
    {
        var school = await dbContext.Schools.SingleOrDefaultAsync(
            item => item.Slug == slug, cancellationToken);
        if (school is not null)
        {
            return school;
        }

        school = new School(name, slug);
        dbContext.Schools.Add(school);
        await dbContext.SaveChangesAsync(cancellationToken);
        return school;
    }

    /// <summary>Creates a hashed demo account and administrator membership when either is missing.</summary>
    private async Task CreateAdminAsync(
        EduCoreDbContext dbContext,
        School school,
        string usernameSetting,
        string passwordSetting,
        string subjectId,
        CancellationToken cancellationToken)
    {
        var userName = configuration[usernameSetting];
        var password = configuration[passwordSetting];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) ||
            password.Length < 12)
        {
            throw new InvalidOperationException(
                $"{usernameSetting} and {passwordSetting} must be set; demo passwords must be at least 12 characters.");
        }

        var existing = await dbContext.DemoCredentials.SingleOrDefaultAsync(
            account => account.NormalizedUserName == userName.Trim().ToUpperInvariant(),
            cancellationToken);
        if (existing is null)
        {
            var credential = new DemoCredential(userName.Trim(), subjectId, string.Empty);
            credential.SetPasswordHash(passwordHasher.HashPassword(credential, password));
            dbContext.DemoCredentials.Add(credential);
            dbContext.SchoolMemberships.Add(new SchoolMembership(
                school.Id, subjectId, SchoolRole.SchoolAdministrator));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!await dbContext.SchoolMemberships.AnyAsync(
                     membership => membership.SubjectId == existing.SubjectId &&
                                   membership.SchoolId == school.Id,
                     cancellationToken))
        {
            dbContext.SchoolMemberships.Add(new SchoolMembership(
                school.Id, existing.SubjectId, SchoolRole.SchoolAdministrator));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Creates a Development-only teacher or student login and its school membership.</summary>
    private async Task CreateRoleAccountAsync(
        EduCoreDbContext dbContext,
        School school,
        string usernameSetting,
        string passwordSetting,
        string subjectId,
        SchoolRole role,
        CancellationToken cancellationToken)
    {
        var userName = configuration[usernameSetting];
        var password = configuration[passwordSetting];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) ||
            password.Length < 12)
        {
            throw new InvalidOperationException(
                $"{usernameSetting} and {passwordSetting} must be set; demo passwords must be at least 12 characters.");
        }

        var normalizedUserName = userName.Trim().ToUpperInvariant();
        var existing = await dbContext.DemoCredentials.SingleOrDefaultAsync(
            account => account.NormalizedUserName == normalizedUserName,
            cancellationToken);
        if (existing is null)
        {
            var credential = new DemoCredential(userName.Trim(), subjectId, string.Empty);
            credential.SetPasswordHash(passwordHasher.HashPassword(credential, password));
            dbContext.DemoCredentials.Add(credential);
        }
        else if (existing.SubjectId != subjectId)
        {
            throw new InvalidOperationException(
                $"The configured demo username '{userName}' is already linked to another identity.");
        }

        if (!await dbContext.SchoolMemberships.AnyAsync(
                membership => membership.SubjectId == subjectId &&
                              membership.SchoolId == school.Id,
                cancellationToken))
        {
            dbContext.SchoolMemberships.Add(new SchoolMembership(school.Id, subjectId, role));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Optionally provisions a separate local platform administrator from runtime secrets.</summary>
    private async Task CreatePlatformAdminAsync(
        EduCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userName = configuration["DemoAuth:PlatformAdmin"];
        var password = configuration["DemoAuth:PlatformAdminPassword"];
        if (string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) ||
            password.Length < 12)
        {
            throw new InvalidOperationException(
                "DemoAuth:PlatformAdmin and DemoAuth:PlatformAdminPassword must both be set; passwords must be at least 12 characters.");
        }

        const string subjectId = "edu-core-demo-platform-admin";
        var normalizedUserName = userName.Trim().ToUpperInvariant();
        var credential = await dbContext.DemoCredentials.SingleOrDefaultAsync(
            account => account.NormalizedUserName == normalizedUserName,
            cancellationToken);
        if (credential is null)
        {
            credential = new DemoCredential(userName.Trim(), subjectId, string.Empty);
            credential.SetPasswordHash(passwordHasher.HashPassword(credential, password));
            dbContext.DemoCredentials.Add(credential);
        }
        else if (credential.SubjectId != subjectId)
        {
            throw new InvalidOperationException(
                "The configured platform administrator username is already linked to another identity.");
        }

        var existingPermissions = await dbContext.PlatformAdministratorGrants
            .Where(grant => grant.SubjectId == subjectId)
            .Select(grant => grant.Permission)
            .ToListAsync(cancellationToken);
        var newPermissions = PlatformPermissions.Bootstrap
            .Except(existingPermissions, StringComparer.Ordinal)
            .ToArray();
        dbContext.PlatformAdministratorGrants.AddRange(newPermissions.Select(permission =>
            new PlatformAdministratorGrant(subjectId, permission)));
        if (newPermissions.Length > 0)
        {
            dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent(
                "system.demo-seed", "platform.admin.provisioned", "identity", subjectId,
                "Development-only platform capabilities were seeded."));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Seeds one assigned class and links its teacher and sample student identities.</summary>
    private async Task SeedNorthfieldLearningAsync(
        School school,
        CancellationToken cancellationToken)
    {
        var tenant = new TenantContext();
        tenant.Establish(school.Id, SchoolRole.SchoolAdministrator);
        using var dbContext = new EduCoreDbContext(dbOptions, tenant);
        var schoolClass = await dbContext.Classes.SingleOrDefaultAsync(
            item => item.Name == "Grade 8 Blue", cancellationToken);
        if (schoolClass is null)
        {
            schoolClass = new SchoolClass(school.Id, "Grade 8 Blue", "Grade 8");
            dbContext.Classes.Add(schoolClass);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var teacher = await dbContext.Teachers.SingleOrDefaultAsync(
            item => item.SubjectId == "north-teacher", cancellationToken);
        if (teacher is null)
        {
            teacher = new Teacher(school.Id, "north-teacher", "Morgan", "Teacher");
            dbContext.Teachers.Add(teacher);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.TeachingAssignments.AnyAsync(
                item => item.TeacherId == teacher.Id &&
                        item.ClassId == schoolClass.Id &&
                        item.SubjectName == "Mathematics",
                cancellationToken))
        {
            dbContext.TeachingAssignments.Add(new TeachingAssignment(
                school.Id, teacher.Id, schoolClass.Id, "Mathematics"));
        }

        var student = await dbContext.Students.SingleAsync(
            item => item.LocalId == "N-1001", cancellationToken);
        if (student.SubjectId is null)
        {
            student.LinkAccount(schoolClass.Id, "north-student");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Seeds one school's sample students idempotently under an explicit tenant context.</summary>
    private async Task SeedStudentsAsync(
        School school, string prefix, CancellationToken cancellationToken)
    {
        var tenant = new TenantContext();
        tenant.Establish(school.Id, SchoolRole.SchoolAdministrator);
        using var dbContext = new EduCoreDbContext(dbOptions, tenant);
        var students = new[]
        {
            new Student(school.Id, $"{prefix}-1001", "Jordan", "Morgan"),
            new Student(school.Id, $"{prefix}-1002", "Taylor", "Reed"),
            new Student(school.Id, $"{prefix}-1003", "Avery", "Patel")
        };
        foreach (var student in students)
        {
            var exists = await dbContext.Students.IgnoreQueryFilters().AnyAsync(
                item => item.SchoolId == school.Id && item.LocalId == student.LocalId,
                cancellationToken);
            if (!exists)
            {
                dbContext.Students.Add(student);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
