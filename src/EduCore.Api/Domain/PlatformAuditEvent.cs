namespace EduCore.Api.Domain;

/// <summary>Immutable record of a platform-level administrative operation.</summary>
public sealed class PlatformAuditEvent
{
    private PlatformAuditEvent()
    {
    }

    /// <summary>Creates a new append-only audit record without sensitive record contents.</summary>
    public PlatformAuditEvent(
        string actorSubjectId,
        string action,
        string targetType,
        string? targetId,
        string? details)
    {
        Id = Guid.NewGuid();
        ActorSubjectId = actorSubjectId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Details = details;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable identity for this audit event.</summary>
    public Guid Id { get; private set; }
    /// <summary>Trusted identity-provider subject that performed the operation.</summary>
    public string ActorSubjectId { get; private set; } = string.Empty;
    /// <summary>Stable action code suitable for filtering.</summary>
    public string Action { get; private set; } = string.Empty;
    /// <summary>Category of the affected platform object.</summary>
    public string TargetType { get; private set; } = string.Empty;
    /// <summary>Non-secret identifier of the affected platform object.</summary>
    public string? TargetId { get; private set; }
    /// <summary>Non-sensitive summary of the change.</summary>
    public string? Details { get; private set; }
    /// <summary>UTC time the event was written.</summary>
    public DateTimeOffset CreatedUtc { get; private set; }
}
