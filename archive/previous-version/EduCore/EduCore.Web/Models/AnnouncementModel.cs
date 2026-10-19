namespace EduCore.Web.Models;

public class AnnouncementModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public DateTime Date { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
