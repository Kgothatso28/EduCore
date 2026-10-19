using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface ISubjectRepository
{
    Task<IEnumerable<SubjectDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<SubjectDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<SubjectDto> AddAsync(SubjectDto subject, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
}
