namespace EduCore.Web.Models;

public class ClassModel
{
    public Guid Id { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
