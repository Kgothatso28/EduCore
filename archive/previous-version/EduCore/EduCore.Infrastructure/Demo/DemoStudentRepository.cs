using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

/// <summary>
/// Demo in-memory student repository used when DemoMode=true. This keeps demo data isolated
/// and easily replaceable by a production repository later.
/// </summary>
public class DemoStudentRepository : IStudentRepository
{
    private readonly List<StudentDto> _students;

    public DemoStudentRepository()
    {
        // Seed demo data
        _students = new List<StudentDto>();
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        for (int i = 1; i <= 30; i++)
        {
            _students.Add(new StudentDto
            {
                Id = Guid.NewGuid(),
                StudentNumber = $"S{1000 + i}",
                FirstName = i % 2 == 0 ? $"John{i}" : $"Jane{i}",
                LastName = "Doe",
                DateOfBirth = DateTime.UtcNow.AddYears(-10).AddDays(i),
                Grade = (i % 7 + 1).ToString(),
                ClassName = $"Grade {(i % 12) + 1}A",
                Gender = i % 2 == 0 ? "Male" : "Female",
                TenantId = i <= 20 ? tenantA : tenantB
            });
        }
    }

    public Task<StudentDto> AddAsync(StudentDto student, CancellationToken cancellationToken = default)
    {
        student.Id = Guid.NewGuid();
        _students.Add(student);
        return Task.FromResult(student);
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var existing = _students.FirstOrDefault(s => s.Id == id && (tenantId == null || s.TenantId == tenantId));
        if (existing == null) return Task.FromResult(false);
        _students.Remove(existing);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<StudentDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<StudentDto> q = _students;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(s => s.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<StudentDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var student = _students.FirstOrDefault(s => s.Id == id && (tenantId == null || s.TenantId == tenantId));
        return Task.FromResult(student);
    }
}
