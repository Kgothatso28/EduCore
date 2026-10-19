using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IStaffRepository
{
    Task<IEnumerable<StaffDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<StaffDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<StaffDto> AddAsync(StaffDto staff, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
}
