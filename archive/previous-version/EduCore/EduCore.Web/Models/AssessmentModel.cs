namespace EduCore.Web.Models;

public class AssessmentModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int MaxMark { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class AssessmentMarkModel
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int Mark { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
