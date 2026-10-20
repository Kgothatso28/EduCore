namespace EduCore.Api.Domain;

/// <summary>Represents a class group owned by one school.</summary>
public sealed class SchoolClass
{
    private SchoolClass()
    {
    }

    /// <summary>Creates a class with a stable school-owned name and grade label.</summary>
    public SchoolClass(Guid schoolId, string name, string gradeLevel)
    {
        Id = Guid.NewGuid();
        SchoolId = schoolId;
        Name = name;
        GradeLevel = gradeLevel;
    }

    /// <summary>Stable identifier scoped by the owning school.</summary>
    public Guid Id { get; private set; }
    /// <summary>School that owns this class record.</summary>
    public Guid SchoolId { get; private set; }
    /// <summary>Display name used to identify the class within its school.</summary>
    public string Name { get; private set; } = string.Empty;
    /// <summary>Grade or year group for this class.</summary>
    public string GradeLevel { get; private set; } = string.Empty;
}
