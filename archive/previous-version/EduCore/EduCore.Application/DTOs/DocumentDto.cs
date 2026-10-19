namespace EduCore.Application.DTOs;

public class DocumentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public string FileType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string TenantId { get; set; } = string.Empty;
}
