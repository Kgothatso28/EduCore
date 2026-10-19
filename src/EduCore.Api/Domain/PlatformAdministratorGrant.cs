namespace EduCore.Api.Domain;

/// <summary>Represents a platform-level permission, separate from any school membership.</summary>
public sealed class PlatformAdministratorGrant
{
    // EF Core uses this constructor when materializing a persisted grant.
    private PlatformAdministratorGrant()
    {
    }

    /// <summary>Stable database identity for this grant.</summary>
    public Guid Id { get; private set; }
    /// <summary>Trusted identity subject receiving the platform-level permission.</summary>
    public string SubjectId { get; private set; } = string.Empty;
    /// <summary>Platform permission name; this is not a school role.</summary>
    public string Permission { get; private set; } = string.Empty;
}
