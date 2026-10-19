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
    /// <summary>Applies migrations, provisions two school admins, and seeds isolated sample students.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            configuration.GetConnectionString("EduCore"));
        if (!connection.InitialCatalog.StartsWith("EduCore_Demo_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Demo seeding is restricted to a database named EduCore_Demo_*.");
        }

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
            await transaction.CommitAsync(cancellationToken);
        }

        await SeedStudentsAsync(north, "N", cancellationToken);
        await SeedStudentsAsync(south, "S", cancellationToken);
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
