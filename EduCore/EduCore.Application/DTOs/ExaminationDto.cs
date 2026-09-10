namespace EduCore.Application.DTOs;

public class ExaminationDto
{
    public Guid Id { get; set; }
    public string Period { get; set; } = string.Empty; // e.g., 2026 Term 1
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Venue { get; set; } = string.Empty;
    public Guid InvigilatorId { get; set; }
    public string InvigilatorName { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled"; // Scheduled, Completed, Cancelled
    public string TenantId { get; set; } = string.Empty;
}
