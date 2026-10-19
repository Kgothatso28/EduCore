using EduCore.Api.Domain;

namespace EduCore.Api.Security;

/// <summary>Holds the validated school and role for one dependency-injection request scope.</summary>
public sealed class TenantContext
{
    /// <summary>Validated active school for this request, or null before membership validation.</summary>
    public Guid? SchoolId { get; private set; }
    /// <summary>Role confirmed for the active school, or null before validation.</summary>
    public SchoolRole? Role { get; private set; }
    /// <summary>Whether the resolver has established a school for this request.</summary>
    public bool IsValidated => SchoolId.HasValue;

    /// <summary>Sets the tenant once after database-backed membership validation.</summary>
    internal void Establish(Guid schoolId, SchoolRole role)
    {
        if (SchoolId.HasValue)
        {
            throw new InvalidOperationException("The school context is already established.");
        }

        SchoolId = schoolId;
        Role = role;
    }
}
