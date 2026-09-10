using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoClassRepository : IClassRepository
{
    private readonly List<ClassDto> _classes;

    public DemoClassRepository()
    {
        _classes = new List<ClassDto>();
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        for (int i = 1; i <= 12; i++)
        {
            _classes.Add(new ClassDto
            {
                Id = Guid.NewGuid(),
                ClassCode = $"C{i:00}",
                Name = $"Grade {i}A",
                Grade = i.ToString(),
                TenantId = i <= 8 ? tenantA : tenantB
            });
        }
    }

    public Task<ClassDto> AddAsync(ClassDto cls, CancellationToken cancellationToken = default)
    {
        cls.Id = Guid.NewGuid();
        _classes.Add(cls);
        return Task.FromResult(cls);
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var existing = _classes.FirstOrDefault(c => c.Id == id && (tenantId == null || c.TenantId == tenantId));
        if (existing == null) return Task.FromResult(false);
        _classes.Remove(existing);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<ClassDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<ClassDto> q = _classes;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(c => c.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<ClassDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var c = _classes.FirstOrDefault(x => x.Id == id && (tenantId == null || x.TenantId == tenantId));
        return Task.FromResult(c);
    }
}
