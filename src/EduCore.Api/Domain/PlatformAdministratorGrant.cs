namespace EduCore.Api.Domain;

/// <summary>Represents a platform-level permission, separate from any school membership.</summary>
public sealed class PlatformAdministratorGrant
{
    // EF Core uses this constructor when materializing a persisted grant.
    private PlatformAdministratorGrant()
    {
    }

    /// <summary>Creates a platform capability grant for a trusted identity-provider subject.</summary>
    public PlatformAdministratorGrant(string subjectId, string permission)
    {
        if (string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("A subject and permission are required.");
        }

        Id = Guid.NewGuid();
        SubjectId = subjectId;
        Permission = permission;
    }

    /// <summary>Stable database identity for this grant.</summary>
    public Guid Id { get; private set; }
    /// <summary>Trusted identity subject receiving the platform-level permission.</summary>
    public string SubjectId { get; private set; } = string.Empty;
    /// <summary>Platform permission name; this is not a school role.</summary>
    public string Permission { get; private set; } = string.Empty;
}
