namespace EduCore.Application.DTOs;

public enum AttendanceStatus
{
    Present,
    Absent,
    Late,
    Excused
}

public class AttendanceDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public Guid? ClassId { get; set; }
    public string? ClassName { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
