using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoStaffRepository : IStaffRepository
{
    private readonly List<StaffDto> _staff;

    public DemoStaffRepository()
    {
        _staff = new List<StaffDto>();
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        for (int i = 1; i <= 15; i++)
        {
            _staff.Add(new StaffDto
            {
                Id = Guid.NewGuid(),
                StaffNumber = $"T{2000 + i}",
                FirstName = i % 2 == 0 ? $"Sam{i}" : $"Taylor{i}",
                LastName = "Teacher",
                Email = $"staff{i}@example.com",
                Role = i % 3 == 0 ? "Administrator" : "Teacher",
                TenantId = i <= 10 ? tenantA : tenantB
            });
        }
    }

    public Task<StaffDto> AddAsync(StaffDto staff, CancellationToken cancellationToken = default)
    {
        staff.Id = Guid.NewGuid();
        _staff.Add(staff);
        return Task.FromResult(staff);
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var existing = _staff.FirstOrDefault(s => s.Id == id && (tenantId == null || s.TenantId == tenantId));
        if (existing == null) return Task.FromResult(false);
        _staff.Remove(existing);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<StaffDto>> GetAllAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<StaffDto> q = _staff;
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(s => s.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<StaffDto?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var staff = _staff.FirstOrDefault(s => s.Id == id && (tenantId == null || s.TenantId == tenantId));
        return Task.FromResult(staff);
    }
}
