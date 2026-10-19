using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IStudentRepository
{
    Task<IEnumerable<StudentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<StudentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<StudentDto> AddAsync(StudentDto student, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
}
