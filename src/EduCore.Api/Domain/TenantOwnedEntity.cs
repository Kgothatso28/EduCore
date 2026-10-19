namespace EduCore.Api.Domain;

/// <summary>Base identity and immutable tenant association for school-owned records.</summary>
public abstract class TenantOwnedEntity
{
    // EF Core uses this constructor when materializing a persisted tenant-owned record.
    protected TenantOwnedEntity()
    {
    }

    /// <summary>Assigns a new record identifier and its owning school.</summary>
    protected TenantOwnedEntity(Guid schoolId)
    {
        Id = Guid.NewGuid();
        SchoolId = schoolId;
    }

    /// <summary>Stable identifier for the tenant-owned record.</summary>
    public Guid Id { get; private set; }
    /// <summary>Immutable school owner used by query filters and write validation.</summary>
    public Guid SchoolId { get; private set; }
}
