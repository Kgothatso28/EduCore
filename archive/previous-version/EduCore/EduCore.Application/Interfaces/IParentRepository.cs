using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IParentRepository
{
    Task<IEnumerable<ParentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<ParentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<ParentDto> AddAsync(ParentDto parent, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
}
