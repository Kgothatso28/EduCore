using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IExaminationRepository
{
    Task<IEnumerable<ExaminationDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<ExaminationDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<ExaminationDto> AddAsync(ExaminationDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
}
