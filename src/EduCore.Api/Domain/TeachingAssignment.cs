namespace EduCore.Api.Domain;

/// <summary>Assigns one teacher to a class for a named subject within a school.</summary>
public sealed class TeachingAssignment
{
    private TeachingAssignment()
    {
    }

    /// <summary>Creates a tenant-owned class and subject assignment for a teacher.</summary>
    public TeachingAssignment(Guid schoolId, Guid teacherId, Guid classId, string subjectName)
    {
        Id = Guid.NewGuid();
        SchoolId = schoolId;
        TeacherId = teacherId;
        ClassId = classId;
        SubjectName = subjectName;
    }

    /// <summary>Stable identifier scoped by the owning school.</summary>
    public Guid Id { get; private set; }
    /// <summary>School that owns this assignment.</summary>
    public Guid SchoolId { get; private set; }
    /// <summary>Tenant-owned teacher profile assigned to this class.</summary>
    public Guid TeacherId { get; private set; }
    /// <summary>Tenant-owned class covered by this assignment.</summary>
    public Guid ClassId { get; private set; }
    /// <summary>Subject the teacher covers for this class.</summary>
    public string SubjectName { get; private set; } = string.Empty;
}
