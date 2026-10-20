namespace EduCore.Api.Domain;

/// <summary>Represents a student record owned by exactly one school tenant.</summary>
public sealed class Student : TenantOwnedEntity
{
    // EF Core uses this constructor when materializing a persisted student.
    private Student()
    {
    }

    /// <summary>Creates a student whose tenant ownership is fixed at creation time.</summary>
    public Student(
        Guid schoolId,
        string localId,
        string firstName,
        string lastName,
        Guid? classId = null,
        string? subjectId = null)
        : base(schoolId)
    {
        LocalId = localId;
        FirstName = firstName;
        LastName = lastName;
        ClassId = classId;
        SubjectId = subjectId;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Student identifier assigned by the owning school.</summary>
    public string LocalId { get; private set; } = string.Empty;
    /// <summary>Student's given name.</summary>
    public string FirstName { get; private set; } = string.Empty;
    /// <summary>Student's family name.</summary>
    public string LastName { get; private set; } = string.Empty;
    /// <summary>Optional class placement; null means the student is not yet assigned.</summary>
    public Guid? ClassId { get; private set; }
    /// <summary>Optional login subject linked to this student record.</summary>
    public string? SubjectId { get; private set; }
    /// <summary>UTC creation time for this student record.</summary>
    public DateTimeOffset CreatedUtc { get; private set; }

    /// <summary>Updates student details while preserving the owning school identifier.</summary>
    public void Update(string localId, string firstName, string lastName)
    {
        LocalId = localId;
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>Links a school-created student record to its student login and class placement.</summary>
    public void LinkAccount(Guid classId, string subjectId)
    {
        ClassId = classId;
        SubjectId = subjectId;
    }
}
