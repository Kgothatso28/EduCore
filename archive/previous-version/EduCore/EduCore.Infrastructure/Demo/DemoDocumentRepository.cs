using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoDocumentRepository : IDocumentRepository
{
    private readonly List<DocumentDto> _docs = new();

    public DemoDocumentRepository()
    {
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        _docs.Add(new DocumentDto { Id = Guid.NewGuid(), Name = "Student Handbook", Category = "Policy", FileType = "pdf", Date = DateTime.UtcNow.AddMonths(-2), Description = "Handbook", Status = "Active", TenantId = tenantA });
        _docs.Add(new DocumentDto { Id = Guid.NewGuid(), Name = "Term Calendar", Category = "Calendar", FileType = "xlsx", Date = DateTime.UtcNow.AddMonths(-1), Description = "Schedule", Status = "Active", TenantId = tenantB });
    }

    public Task<DocumentDto> AddAsync(DocumentDto doc, CancellationToken cancellationToken = default)
    {
        doc.Id = Guid.NewGuid();
        _docs.Add(doc);
        return Task.FromResult(doc);
    }

    public Task DeleteAsync(Guid id)
    {
        var d = _docs.FirstOrDefault(x => x.Id == id);
        if (d != null) _docs.Remove(d);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DocumentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _docs.AsEnumerable();
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(d => d.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<DocumentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var d = _docs.FirstOrDefault(x => x.Id == id);
        if (!string.IsNullOrEmpty(tenantId) && d != null && d.TenantId != tenantId) return Task.FromResult<DocumentDto?>(null);
        return Task.FromResult(d);
    }
}
