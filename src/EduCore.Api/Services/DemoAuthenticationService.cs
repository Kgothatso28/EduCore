using System.Security.Claims;
using EduCore.Api.Data;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Services;

/// <summary>Authenticates local demo users and resolves their permitted school memberships.</summary>
public sealed class DemoAuthenticationService(
    EduCoreDbContext dbContext,
    IPasswordHasher<DemoCredential> passwordHasher)
{
    /// <summary>Checks an active account's password hash without returning credentials on failure.</summary>
    public async Task<DemoCredential?> VerifyAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = userName.Trim().ToUpperInvariant();
        var credential = await dbContext.DemoCredentials
            .SingleOrDefaultAsync(
                account => account.NormalizedUserName == normalizedUserName && account.IsActive,
                cancellationToken);
        if (credential is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            credential, credential.PasswordHash, password);
        return verification is PasswordVerificationResult.Success or
            PasswordVerificationResult.SuccessRehashNeeded
            ? credential
            : null;
    }

    /// <summary>Lists active school memberships joined to schools that are themselves active.</summary>
    public async Task<IReadOnlyList<SchoolOption>> GetSchoolsAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        return await dbContext.SchoolMemberships
            .Where(membership => membership.SubjectId == subjectId && membership.IsActive)
            .Join(dbContext.Schools.Where(school => school.Status == SchoolStatus.Active),
                membership => membership.SchoolId,
                school => school.Id,
                (membership, school) => new { membership, school })
            .OrderBy(item => item.school.Name)
            .Select(item => new SchoolOption(
                item.school.Id, item.school.Name, item.membership.Role))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Confirms current membership and active school status before selection.</summary>
    public async Task<bool> CanAccessSchoolAsync(
        string subjectId,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        return await dbContext.SchoolMemberships.AnyAsync(
            membership => membership.SubjectId == subjectId &&
                          membership.SchoolId == schoolId &&
                          membership.IsActive &&
                          dbContext.Schools.Any(school =>
                              school.Id == schoolId && school.Status == SchoolStatus.Active),
            cancellationToken);
    }

    /// <summary>Creates an authenticated principal with identity claims and optional school selection.</summary>
    public static ClaimsPrincipal CreatePrincipal(DemoCredential credential, Guid? schoolId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, credential.SubjectId),
            new(ClaimTypes.Name, credential.UserName)
        };
        if (schoolId.HasValue)
        {
            claims.Add(new Claim(SchoolContextResolver.ActiveSchoolClaim, schoolId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims, "EduCoreDemoCookie", ClaimTypes.Name, ClaimTypes.Role));
    }

    /// <summary>Public school information returned by the demo school chooser.</summary>
    public sealed record SchoolOption(Guid Id, string Name, SchoolRole Role);
}
