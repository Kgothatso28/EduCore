namespace EduCore.Web.Models;

public class ParentModel
{
    public Guid Id { get; set; }
    public string ParentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
