using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IAssessmentRepository
{
    Task<IEnumerable<AssessmentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<AssessmentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<AssessmentDto> AddAsync(AssessmentDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<AssessmentMarkDto> AddMarkAsync(AssessmentMarkDto mark, CancellationToken cancellationToken = default);
    Task<IEnumerable<AssessmentMarkDto>> GetMarksByAssessmentAsync(Guid assessmentId, string? tenantId = null, CancellationToken cancellationToken = default);
}
