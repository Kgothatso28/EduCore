namespace EduCore.Application.Abstractions;

public interface ITenantContext
{
    /// <summary>
    /// Current tenant identifier. May be null when anonymous.
    /// </summary>
    string? TenantId { get; }
}
