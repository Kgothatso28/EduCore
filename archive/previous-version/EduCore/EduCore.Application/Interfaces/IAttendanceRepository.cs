using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IAttendanceRepository
{
    Task<IEnumerable<AttendanceDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<AttendanceDto>> GetByStudentAsync(Guid studentId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<AttendanceDto>> GetByClassAsync(Guid classId, DateTime? date = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<AttendanceDto>> GetByDateAsync(DateTime date, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<AttendanceDto> RecordAsync(AttendanceDto attendance, CancellationToken cancellationToken = default);
    Task<AttendanceDto?> UpdateAsync(AttendanceDto attendance, CancellationToken cancellationToken = default);
    Task<IEnumerable<AttendanceSummaryDto>> GetSummaryAsync(DateTime start, DateTime end, string? tenantId = null, CancellationToken cancellationToken = default);
}
