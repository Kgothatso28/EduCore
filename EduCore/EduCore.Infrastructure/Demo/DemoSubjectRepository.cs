using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoSubjectRepository : ISubjectRepository
{
    private readonly List<SubjectDto> _subjects;

    public DemoSubjectRepository()
    {
        _subjects = new List<SubjectDto>();
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        var subjects = new[] { "Mathematics", "English", "Science", "History", "Geography", "Art", "Music" };
        for (int i = 0; i < subjects.Length; i++)
        {
            _subjects.Add(new SubjectDto
            {
                Id = Guid.NewGuid(),
                Code = $"SUB{i+1:00}",
                Name = subjects[i],
                TenantId = i % 2 == 0 ? tenantA : tenantB
            });
        }
    }

    public Task<SubjectDto> AddAsync(SubjectDto subject, CancellationToken cancellationToken = default)
    {
        subject.Id = Guid.NewGuid();
        _subjects.Add(subject);
        return Task.FromResult(subject);
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var existing = _subjects.FirstOrDefault(s => s.Id == id && (tenantId == null || s.TenantId == tenantId));
        if (existing == null) return Task.FromResult(false);
        _subjects.Remove(existing);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<SubjectDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<SubjectDto> q = _subjects;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(s => s.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<SubjectDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var s = _subjects.FirstOrDefault(x => x.Id == id && (tenantId == null || x.TenantId == tenantId));
        return Task.FromResult(s);
    }
}
