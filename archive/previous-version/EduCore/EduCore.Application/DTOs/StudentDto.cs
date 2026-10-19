namespace EduCore.Application.DTOs;

public class StudentDto
{
    public Guid Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
