namespace EduCore.Api.Domain;

/// <summary>Describes whether a school may participate in normal tenant operations.</summary>
public enum SchoolStatus
{
    /// <summary>The school can be selected and used for normal operations.</summary>
    Active,
    /// <summary>The school is temporarily unavailable for tenant requests.</summary>
    Suspended,
    /// <summary>The school is retired from active tenant operations.</summary>
    Archived
}

/// <summary>Represents a school tenant and its lifecycle state.</summary>
public sealed class School
{
    // EF Core uses this constructor when materializing a persisted school.
    private School()
    {
    }

    /// <summary>Creates an active school with a stable identifier and creation timestamp.</summary>
    public School(string name, string slug)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
        Status = SchoolStatus.Active;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable identifier used by school memberships and tenant-owned records.</summary>
    public Guid Id { get; private set; }
    /// <summary>Human-readable school name shown in the application.</summary>
    public string Name { get; private set; } = string.Empty;
    /// <summary>Unique, URL-safe school key used for provisioning and lookup.</summary>
    public string Slug { get; private set; } = string.Empty;
    /// <summary>Lifecycle state checked before a school context is established.</summary>
    public SchoolStatus Status { get; private set; }
    /// <summary>UTC creation time for the school record.</summary>
    public DateTimeOffset CreatedUtc { get; private set; }

    /// <summary>Prevents this school from being selected for validated tenant requests.</summary>
    public void Suspend() => Status = SchoolStatus.Suspended;

    /// <summary>Reactivates a suspended school without changing its data or memberships.</summary>
    public void Reactivate()
    {
        if (Status == SchoolStatus.Archived)
        {
            throw new InvalidOperationException("An archived school cannot be reactivated.");
        }

        Status = SchoolStatus.Active;
    }

    /// <summary>Marks the school archived so it is excluded from active-school resolution.</summary>
    public void Archive() => Status = SchoolStatus.Archived;
}
