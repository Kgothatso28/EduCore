using EduCore.Api.Data;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Services;

/// <summary>Implements student persistence operations within the validated tenant context.</summary>
public sealed class StudentService(EduCoreDbContext dbContext, TenantContext tenantContext)
{
    /// <summary>Returns the active school's students ordered by their local identifiers.</summary>
    public async Task<IReadOnlyList<Student>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Students.AsNoTracking().OrderBy(student => student.LocalId)
            .ToListAsync(cancellationToken);

    /// <summary>Filters, counts, and returns a bounded page of active-school student records.</summary>
    public async Task<StudentPage> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Min(page, int.MaxValue / pageSize);
        var query = dbContext.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(student =>
                student.LocalId.Contains(term) ||
                student.FirstName.Contains(term) ||
                student.LastName.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(student => student.LocalId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new StudentPage(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    /// <summary>Finds a student only if it belongs to the current tenant.</summary>
    public Task<Student?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Students.SingleOrDefaultAsync(student => student.Id == id, cancellationToken);

    /// <summary>Validates fields and creates a student using the server-established school ID.</summary>
    public async Task<Student> CreateAsync(
        string localId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        EnsureValid(localId, firstName, lastName);
        var schoolId = tenantContext.SchoolId ??
            throw new InvalidOperationException("A validated school context is required.");
        var student = new Student(schoolId, localId.Trim(), firstName.Trim(), lastName.Trim());
        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(cancellationToken);
        return student;
    }

    /// <summary>Updates visible student fields without allowing tenant ownership to change.</summary>
    public async Task<bool> UpdateAsync(
        Guid id,
        string localId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        EnsureValid(localId, firstName, lastName);
        var student = await dbContext.Students.SingleOrDefaultAsync(
            record => record.Id == id, cancellationToken);
        if (student is null)
        {
            return false;
        }

        student.Update(localId.Trim(), firstName.Trim(), lastName.Trim());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Deletes a student only when it is visible in the current tenant.</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.SingleOrDefaultAsync(
            record => record.Id == id, cancellationToken);
        if (student is null)
        {
            return false;
        }

        dbContext.Students.Remove(student);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Enforces required student text and the database column length limits.</summary>
    private static void EnsureValid(string localId, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(localId) || localId.Trim().Length > 80 ||
            string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 100)
        {
            throw new ArgumentException("Student identifier and names are required and must fit their limits.");
        }
    }

    /// <summary>Packages one result page with the values required for client navigation.</summary>
    public sealed record StudentPage(
        IReadOnlyList<Student> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
