using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IClassRepository
{
    Task<IEnumerable<ClassDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<ClassDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<ClassDto> AddAsync(ClassDto cls, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
}
