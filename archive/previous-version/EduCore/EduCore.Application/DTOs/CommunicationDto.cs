namespace EduCore.Application.DTOs;

public class AnnouncementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal"; // Normal, Important, Urgent
    public DateTime Date { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Read { get; set; }
    public DateTime Date { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
