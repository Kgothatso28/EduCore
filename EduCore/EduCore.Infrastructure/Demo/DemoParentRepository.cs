using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoParentRepository : IParentRepository
{
    private readonly List<ParentDto> _parents;

    public DemoParentRepository()
    {
        _parents = new List<ParentDto>();
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        for (int i = 1; i <= 20; i++)
        {
            _parents.Add(new ParentDto
            {
                Id = Guid.NewGuid(),
                ParentNumber = $"P{1000 + i}",
                FirstName = i % 2 == 0 ? $"Alex{i}" : $"Pat{i}",
                LastName = "Guardian",
                Email = $"parent{i}@example.com",
                Phone = $"+10000000{100 + i}",
                TenantId = i <= 12 ? tenantA : tenantB
            });
        }
    }

    public Task<ParentDto> AddAsync(ParentDto parent, CancellationToken cancellationToken = default)
    {
        parent.Id = Guid.NewGuid();
        _parents.Add(parent);
        return Task.FromResult(parent);
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var existing = _parents.FirstOrDefault(p => p.Id == id && (tenantId == null || p.TenantId == tenantId));
        if (existing == null) return Task.FromResult(false);
        _parents.Remove(existing);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<ParentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<ParentDto> q = _parents;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(p => p.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<ParentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var parent = _parents.FirstOrDefault(p => p.Id == id && (tenantId == null || p.TenantId == tenantId));
        return Task.FromResult(parent);
    }
}
