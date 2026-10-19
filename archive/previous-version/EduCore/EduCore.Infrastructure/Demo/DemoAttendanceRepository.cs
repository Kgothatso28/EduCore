using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

/// <summary>
/// Demo in-memory attendance repository seeded with realistic data for two tenants.
/// Used when DemoMode=true to provide interactive demo data without a real database.
/// </summary>
public class DemoAttendanceRepository : IAttendanceRepository
{
    private readonly List<AttendanceDto> _records;

    public DemoAttendanceRepository()
    {
        _records = new List<AttendanceDto>();
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        // Use existing demo students from DemoStudentRepository if available, otherwise create placeholder student ids/names
        var rnd = new Random(42);
        var startDate = DateTime.UtcNow.Date.AddDays(-30);

        // Create 10 students per tenant
        var studentsA = Enumerable.Range(1, 10).Select(i => (Id: Guid.NewGuid(), Name: $"StudentA{i}")).ToList();
        var studentsB = Enumerable.Range(1, 8).Select(i => (Id: Guid.NewGuid(), Name: $"StudentB{i}")).ToList();

        // Create classes
        var classA = (Id: Guid.NewGuid(), Name: "Grade 1A");
        var classB = (Id: Guid.NewGuid(), Name: "Grade 2B");

        for (int d = 0; d < 21; d++)
        {
            var date = startDate.AddDays(d);
            foreach (var s in studentsA)
            {
                var status = (AttendanceStatus)rnd.Next(0, 4);
                _records.Add(new AttendanceDto
                {
                    Id = Guid.NewGuid(),
                    StudentId = s.Id,
                    StudentName = s.Name,
                    ClassId = classA.Id,
                    ClassName = classA.Name,
                    Date = date,
                    Status = status,
                    TenantId = tenantA
                });
            }

            foreach (var s in studentsB)
            {
                var status = (AttendanceStatus)rnd.Next(0, 4);
                _records.Add(new AttendanceDto
                {
                    Id = Guid.NewGuid(),
                    StudentId = s.Id,
                    StudentName = s.Name,
                    ClassId = classB.Id,
                    ClassName = classB.Name,
                    Date = date,
                    Status = status,
                    TenantId = tenantB
                });
            }
        }
    }

    public Task<AttendanceDto> RecordAsync(AttendanceDto attendance, CancellationToken cancellationToken = default)
    {
        attendance.Id = Guid.NewGuid();
        _records.Add(attendance);
        return Task.FromResult(attendance);
    }

    public Task<AttendanceDto?> UpdateAsync(AttendanceDto attendance, CancellationToken cancellationToken = default)
    {
        var existing = _records.FirstOrDefault(r => r.Id == attendance.Id && r.StudentId == attendance.StudentId && r.Date.Date == attendance.Date.Date && r.TenantId == attendance.TenantId);
        if (existing == null) return Task.FromResult<AttendanceDto?>(null);
        existing.Status = attendance.Status;
        existing.ClassId = attendance.ClassId;
        existing.ClassName = attendance.ClassName;
        return Task.FromResult<AttendanceDto?>(existing);
    }

    public Task<IEnumerable<AttendanceDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<AttendanceDto> q = _records;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(r => r.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<IEnumerable<AttendanceDto>> GetByClassAsync(Guid classId, DateTime? date = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _records.Where(r => r.ClassId == classId && (date == null || r.Date.Date == date.Value.Date));
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(r => r.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<IEnumerable<AttendanceDto>> GetByStudentAsync(Guid studentId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _records.Where(r => r.StudentId == studentId);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(r => r.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<IEnumerable<AttendanceDto>> GetByDateAsync(DateTime date, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _records.Where(r => r.Date.Date == date.Date);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(r => r.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<IEnumerable<AttendanceSummaryDto>> GetSummaryAsync(DateTime start, DateTime end, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _records.Where(r => r.Date.Date >= start.Date && r.Date.Date <= end.Date);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(r => r.TenantId == tenantId);

        var groups = q.GroupBy(r => r.StudentId).Select(g => new AttendanceSummaryDto
        {
            StudentId = g.Key,
            StudentName = g.First().StudentName,
            TotalDays = g.Count(),
            PresentDays = g.Count(x => x.Status == AttendanceStatus.Present),
            AbsentDays = g.Count(x => x.Status == AttendanceStatus.Absent),
            LateDays = g.Count(x => x.Status == AttendanceStatus.Late),
            ExcusedDays = g.Count(x => x.Status == AttendanceStatus.Excused)
        });

        return Task.FromResult(groups);
    }
}
