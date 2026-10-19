namespace EduCore.Api.Domain;

/// <summary>Stores a local-demo login identity and its password hash.</summary>
public sealed class DemoCredential
{
    // EF Core uses this constructor when materializing a persisted credential.
    private DemoCredential()
    {
    }

    /// <summary>Creates an active demo credential with a normalized username.</summary>
    public DemoCredential(string userName, string subjectId, string passwordHash)
    {
        Id = Guid.NewGuid();
        UserName = userName;
        NormalizedUserName = userName.Trim().ToUpperInvariant();
        SubjectId = subjectId;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    /// <summary>Stable database identity for this credential.</summary>
    public Guid Id { get; private set; }
    /// <summary>Username displayed to the local demo user.</summary>
    public string UserName { get; private set; } = string.Empty;
    /// <summary>Uppercase lookup key used to enforce case-insensitive sign-in.</summary>
    public string NormalizedUserName { get; private set; } = string.Empty;
    /// <summary>Subject identifier used to associate the login with school memberships.</summary>
    public string SubjectId { get; private set; } = string.Empty;
    /// <summary>One-way password hash; plaintext passwords are never stored.</summary>
    public string PasswordHash { get; private set; } = string.Empty;
    /// <summary>Whether this demo account may authenticate.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Replaces the stored hash without exposing plaintext password state.</summary>
    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
