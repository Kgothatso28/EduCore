namespace EduCore.Api.Domain;

/// <summary>Defines the roles a subject may hold within a single school tenant.</summary>
public enum SchoolRole
{
    /// <summary>May administer school configuration and student records.</summary>
    SchoolAdministrator,
    /// <summary>School teaching role; it does not grant administrator operations.</summary>
    Teacher,
    /// <summary>General school staff role.</summary>
    Staff,
    /// <summary>Student self-service role.</summary>
    Student,
    /// <summary>Guardian access associated with a student's family relationship.</summary>
    Guardian
}

/// <summary>Associates an identity-provider subject with a role in one school.</summary>
public sealed class SchoolMembership
{
    // EF Core uses this constructor when materializing a persisted membership.
    private SchoolMembership()
    {
    }

    /// <summary>Creates an active membership for a trusted identity subject.</summary>
    public SchoolMembership(Guid schoolId, string subjectId, SchoolRole role)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("A trusted identity subject is required.", nameof(subjectId));
        }

        Id = Guid.NewGuid();
        SchoolId = schoolId;
        SubjectId = subjectId;
        Role = role;
        IsActive = true;
    }

    /// <summary>Stable identifier for this membership record.</summary>
    public Guid Id { get; private set; }
    /// <summary>School that scopes this role assignment.</summary>
    public Guid SchoolId { get; private set; }
    /// <summary>Trusted identity subject to which the membership belongs.</summary>
    public string SubjectId { get; private set; } = string.Empty;
    /// <summary>Permissions category assigned within this school.</summary>
    public SchoolRole Role { get; private set; }
    /// <summary>Whether this membership can establish an active school context.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Revokes this membership so it can no longer establish tenant context.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Restores a previously deactivated membership.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Changes this identity's school-local role without changing its platform grants.</summary>
    public void ChangeRole(SchoolRole role) => Role = role;
}
