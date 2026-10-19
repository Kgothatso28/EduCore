namespace EduCore.Application.DTOs;

public class AttendanceSummaryDto
{
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }
    public int TotalDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public int ExcusedDays { get; set; }
    public double AttendancePercentage => TotalDays == 0 ? 0 : (double)PresentDays / TotalDays * 100.0;
}
