using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IDocumentRepository
{
    Task<IEnumerable<DocumentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<DocumentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<DocumentDto> AddAsync(DocumentDto doc, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id);
}
