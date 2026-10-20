namespace EduCore.Api.Domain;

/// <summary>Authentication outcome captured by an authentication flow managed by EduCore.</summary>
public sealed class PlatformLoginEvent
{
    private PlatformLoginEvent()
    {
    }

    /// <summary>Creates an authentication history entry without password or token material.</summary>
    public PlatformLoginEvent(string accountIdentifier, bool succeeded, string outcome)
    {
        Id = Guid.NewGuid();
        AccountIdentifier = accountIdentifier;
        Succeeded = succeeded;
        Outcome = outcome;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable identity for this authentication event.</summary>
    public Guid Id { get; private set; }
    /// <summary>Submitted username or trusted subject identifier, truncated by the caller.</summary>
    public string AccountIdentifier { get; private set; } = string.Empty;
    /// <summary>Whether the authentication attempt succeeded.</summary>
    public bool Succeeded { get; private set; }
    /// <summary>Coarse outcome category; never contains submitted secrets.</summary>
    public string Outcome { get; private set; } = string.Empty;
    /// <summary>UTC time the attempt was recorded.</summary>
    public DateTimeOffset CreatedUtc { get; private set; }
}
