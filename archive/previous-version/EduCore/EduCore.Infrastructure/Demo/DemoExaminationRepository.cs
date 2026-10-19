using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoExaminationRepository : IExaminationRepository
{
    private readonly List<ExaminationDto> _exams = new();

    public DemoExaminationRepository()
    {
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        _exams.Add(new ExaminationDto
        {
            Id = Guid.NewGuid(),
            Period = "2026 Term 1",
            SubjectId = Guid.NewGuid(),
            SubjectName = "Mathematics",
            ClassId = Guid.NewGuid(),
            ClassName = "Grade 1A",
            StartTime = DateTime.UtcNow.AddDays(7).AddHours(9),
            EndTime = DateTime.UtcNow.AddDays(7).AddHours(11),
            Venue = "Hall A",
            InvigilatorId = Guid.NewGuid(),
            InvigilatorName = "Ms. Smith",
            Status = "Scheduled",
            TenantId = tenantA
        });

        _exams.Add(new ExaminationDto
        {
            Id = Guid.NewGuid(),
            Period = "2026 Term 1",
            SubjectId = Guid.NewGuid(),
            SubjectName = "English",
            ClassId = Guid.NewGuid(),
            ClassName = "Grade 2B",
            StartTime = DateTime.UtcNow.AddDays(10).AddHours(13),
            EndTime = DateTime.UtcNow.AddDays(10).AddHours(15),
            Venue = "Room 101",
            InvigilatorId = Guid.NewGuid(),
            InvigilatorName = "Mr. Jones",
            Status = "Scheduled",
            TenantId = tenantB
        });
    }

    public Task<ExaminationDto> AddAsync(ExaminationDto dto, CancellationToken cancellationToken = default)
    {
        dto.Id = Guid.NewGuid();
        _exams.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var ex = _exams.FirstOrDefault(e => e.Id == id && (tenantId == null || e.TenantId == tenantId));
        if (ex == null) return Task.FromResult(false);
        _exams.Remove(ex);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<ExaminationDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<ExaminationDto> q = _exams;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(e => e.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<ExaminationDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var e = _exams.FirstOrDefault(x => x.Id == id && (tenantId == null || x.TenantId == tenantId));
        return Task.FromResult(e);
    }
}
