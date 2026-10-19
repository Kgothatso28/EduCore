namespace EduCore.Application.DTOs;

public class ResultDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int TotalMarks { get; set; }
    public int MaxMarks { get; set; }
    public double Percentage => MaxMarks == 0 ? 0 : (TotalMarks * 100.0 / MaxMarks);
    public string Grade { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string AcademicPeriod { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
