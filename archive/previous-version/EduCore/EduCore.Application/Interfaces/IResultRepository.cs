using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IResultRepository
{
    Task<IEnumerable<ResultDto>> GetResultsByStudentAsync(Guid studentId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<ResultDto>> GetResultsByClassAsync(Guid classId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<ResultDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
}
