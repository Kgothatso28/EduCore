namespace EduCore.Api.Domain;

/// <summary>Links a school teacher identity to a tenant-owned staff profile.</summary>
public sealed class Teacher
{
    private Teacher()
    {
    }

    /// <summary>Creates the school profile associated with a trusted identity subject.</summary>
    public Teacher(Guid schoolId, string subjectId, string firstName, string lastName)
    {
        Id = Guid.NewGuid();
        SchoolId = schoolId;
        SubjectId = subjectId;
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>Stable identifier scoped by the owning school.</summary>
    public Guid Id { get; private set; }
    /// <summary>School that owns this teacher profile.</summary>
    public Guid SchoolId { get; private set; }
    /// <summary>Identity subject that must match the validated teacher membership.</summary>
    public string SubjectId { get; private set; } = string.Empty;
    /// <summary>Teacher's given name.</summary>
    public string FirstName { get; private set; } = string.Empty;
    /// <summary>Teacher's family name.</summary>
    public string LastName { get; private set; } = string.Empty;
}
