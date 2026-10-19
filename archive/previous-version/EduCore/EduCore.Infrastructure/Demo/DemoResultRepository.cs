using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoResultRepository : IResultRepository
{
    private readonly List<ResultDto> _results = new();

    public DemoResultRepository()
    {
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        // Create demo results for a few students/subjects
        for (int i = 1; i <= 6; i++)
        {
            _results.Add(new ResultDto
            {
                Id = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                StudentName = "StudentA" + i,
                SubjectId = Guid.NewGuid(),
                SubjectName = "Mathematics",
                TotalMarks = 70 + i,
                MaxMarks = 100,
                Grade = "B",
                Passed = true,
                AcademicPeriod = "2026 Term 1",
                TenantId = tenantA
            });
        }

        for (int i = 1; i <= 4; i++)
        {
            _results.Add(new ResultDto
            {
                Id = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                StudentName = "StudentB" + i,
                SubjectId = Guid.NewGuid(),
                SubjectName = "Science",
                TotalMarks = 40 + i,
                MaxMarks = 50,
                Grade = "A",
                Passed = true,
                AcademicPeriod = "2026 Term 1",
                TenantId = tenantB
            });
        }
    }

    public Task<IEnumerable<ResultDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<ResultDto> q = _results;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(r => r.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<IEnumerable<ResultDto>> GetResultsByClassAsync(Guid classId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Demo repository does not map class->results; return all for tenant as simple demo
        return GetAllAsync(tenantId, cancellationToken);
    }

    public Task<IEnumerable<ResultDto>> GetResultsByStudentAsync(Guid studentId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _results.Where(r => r.StudentId == studentId);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(r => r.TenantId == tenantId);
        return Task.FromResult(q);
    }
}
