namespace EduCore.Application.DTOs;

/// <summary>
/// Data Transfer Object representing a parent/guardian used across the API and demo repositories.
/// Kept simple for DemoMode and intended to be serializable to/from JSON.
/// </summary>
public class ParentDto
{
    /// <summary>Unique identifier for the parent record.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable parent number used in the demo UI.</summary>
    public string ParentNumber { get; set; } = string.Empty;

    /// <summary>Given name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Contact email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Contact phone number.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Tenant identifier used for multi-tenant filtering in DemoMode.</summary>
    public string TenantId { get; set; } = string.Empty;
}
