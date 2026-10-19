using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoAssessmentRepository : IAssessmentRepository
{
    private readonly List<AssessmentDto> _assessments = new();
    private readonly List<AssessmentMarkDto> _marks = new();

    public DemoAssessmentRepository()
    {
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        var subjA = Guid.NewGuid();
        var subjB = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();

        _assessments.Add(new AssessmentDto
        {
            Id = Guid.NewGuid(),
            Title = "Term 1 Math Test",
            SubjectId = subjA,
            SubjectName = "Mathematics",
            ClassId = classA,
            ClassName = "Grade 1A",
            TeacherId = Guid.NewGuid(),
            TeacherName = "Ms. Smith",
            Type = "Test",
            Date = DateTime.UtcNow.AddDays(-7),
            MaxMark = 100,
            TenantId = tenantA
        });

        _assessments.Add(new AssessmentDto
        {
            Id = Guid.NewGuid(),
            Title = "Project - Science",
            SubjectId = subjB,
            SubjectName = "Science",
            ClassId = classB,
            ClassName = "Grade 2B",
            TeacherId = Guid.NewGuid(),
            TeacherName = "Mr. Jones",
            Type = "Project",
            Date = DateTime.UtcNow.AddDays(-10),
            MaxMark = 50,
            TenantId = tenantB
        });
    }

    public Task<AssessmentDto> AddAsync(AssessmentDto dto, CancellationToken cancellationToken = default)
    {
        dto.Id = Guid.NewGuid();
        _assessments.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var ex = _assessments.FirstOrDefault(a => a.Id == id && (tenantId == null || a.TenantId == tenantId));
        if (ex == null) return Task.FromResult(false);
        _assessments.Remove(ex);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<AssessmentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<AssessmentDto> q = _assessments;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(a => a.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<AssessmentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var a = _assessments.FirstOrDefault(x => x.Id == id && (tenantId == null || x.TenantId == tenantId));
        return Task.FromResult(a);
    }

    public Task<AssessmentMarkDto> AddMarkAsync(AssessmentMarkDto mark, CancellationToken cancellationToken = default)
    {
        mark.Id = Guid.NewGuid();
        _marks.Add(mark);
        return Task.FromResult(mark);
    }

    public Task<IEnumerable<AssessmentMarkDto>> GetMarksByAssessmentAsync(Guid assessmentId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _marks.Where(m => m.AssessmentId == assessmentId);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(m => m.TenantId == tenantId);
        return Task.FromResult(q);
    }
}
