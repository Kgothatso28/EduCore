using System.Globalization;
using EduCore.Api.Data;
using EduCore.Api.Domain;
using EduCore.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Services;

/// <summary>Builds role-specific dashboard data from the validated school's records.</summary>
public sealed class DashboardService(EduCoreDbContext dbContext, TenantContext tenantContext)
{
    /// <summary>Returns school-wide counts for an administrator's overview.</summary>
    public async Task<PrincipalDashboard?> GetPrincipalAsync(CancellationToken cancellationToken)
    {
        if (!tenantContext.IsValidated)
        {
            return null;
        }

        return new PrincipalDashboard(
            await dbContext.Students.CountAsync(cancellationToken),
            await dbContext.Teachers.CountAsync(cancellationToken),
            await dbContext.Classes.CountAsync(cancellationToken),
            await dbContext.TeachingAssignments.CountAsync(cancellationToken));
    }

    /// <summary>Returns current school counts and SQL-aggregated student cohorts for one calendar year.</summary>
    public async Task<PrincipalAnalytics?> GetPrincipalAnalyticsAsync(
        int year,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.IsValidated)
        {
            return null;
        }

        if (year < 1 || year > DateTime.UtcNow.Year)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        var studentCount = await dbContext.Students.CountAsync(cancellationToken);
        var monthlyCounts = await dbContext.Students
            .Where(student => student.CreatedUtc.Year == year)
            .GroupBy(student => student.CreatedUtc.Month)
            .Select(group => new { Month = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Month, item => item.Count, cancellationToken);
        var monthlyEnrollments = Enumerable.Range(1, 12)
            .Select(month => new MonthlyEnrollment(
                month,
                CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month),
                monthlyCounts.GetValueOrDefault(month)))
            .ToArray();

        var classes = await dbContext.Classes.AsNoTracking()
            .OrderBy(schoolClass => schoolClass.GradeLevel)
            .ThenBy(schoolClass => schoolClass.Name)
            .Select(schoolClass => new ClassEnrollment(
                $"{schoolClass.GradeLevel} · {schoolClass.Name}",
                dbContext.Students.Count(student => student.ClassId == schoolClass.Id)))
            .ToListAsync(cancellationToken);
        var unassignedStudents = await dbContext.Students
            .CountAsync(student => student.ClassId == null, cancellationToken);
        if (unassignedStudents > 0)
        {
            classes.Add(new ClassEnrollment("Unassigned", unassignedStudents));
        }

        return new PrincipalAnalytics(
            year,
            studentCount,
            await dbContext.Teachers.CountAsync(cancellationToken),
            await dbContext.Classes.CountAsync(cancellationToken),
            await dbContext.TeachingAssignments.CountAsync(cancellationToken),
            monthlyEnrollments,
            classes,
            "Monthly bars count currently retained student records by CreatedUtc. Deleted records and withdrawals are not retained, so this is not historical month-end enrolment.");
    }

    /// <summary>Returns only classes and subjects assigned to the signed-in teacher.</summary>
    public async Task<TeacherDashboard?> GetTeacherAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        var teacher = await dbContext.Teachers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.SubjectId == subjectId, cancellationToken);
        if (teacher is null)
        {
            return null;
        }

        var classes = await (
            from assignment in dbContext.TeachingAssignments.AsNoTracking()
            join schoolClass in dbContext.Classes.AsNoTracking()
                on new { assignment.SchoolId, assignment.ClassId }
                equals new { schoolClass.SchoolId, ClassId = schoolClass.Id }
            where assignment.TeacherId == teacher.Id
            orderby schoolClass.Name, assignment.SubjectName
            select new TeacherClass(
                schoolClass.Id,
                schoolClass.Name,
                schoolClass.GradeLevel,
                assignment.SubjectName,
                dbContext.Students.Count(student => student.ClassId == schoolClass.Id)))
            .ToListAsync(cancellationToken);

        var assignedClasses = classes.DistinctBy(item => item.Id).ToArray();
        return new TeacherDashboard(
            $"{teacher.FirstName} {teacher.LastName}",
            classes,
            assignedClasses.Length,
            classes.Select(item => item.SubjectName).Distinct().Count(),
            assignedClasses.Sum(item => item.StudentCount),
            assignedClasses.Select(item => new ClassSize(
                $"{item.GradeLevel} · {item.Name}",
                item.StudentCount)).ToArray());
    }

    /// <summary>Returns a class roster only when the teacher has a matching assignment.</summary>
    public async Task<IReadOnlyList<StudentSummary>?> GetTeacherRosterAsync(
        string subjectId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        var isAssigned = await (
            from assignment in dbContext.TeachingAssignments
            join teacher in dbContext.Teachers
                on new { assignment.SchoolId, assignment.TeacherId }
                equals new { teacher.SchoolId, TeacherId = teacher.Id }
            where teacher.SubjectId == subjectId && assignment.ClassId == classId
            select assignment.Id)
            .AnyAsync(cancellationToken);
        if (!isAssigned)
        {
            return null;
        }

        return await dbContext.Students.AsNoTracking()
            .Where(student => student.ClassId == classId)
            .OrderBy(student => student.LocalId)
            .Select(student => new StudentSummary(
                student.Id, student.LocalId, student.FirstName, student.LastName))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Returns only the student profile linked to the signed-in identity.</summary>
    public async Task<StudentDashboard?> GetStudentAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.AsNoTracking()
            .Where(item => item.SubjectId == subjectId)
            .Select(item => new
            {
                item.LocalId,
                item.FirstName,
                item.LastName,
                item.ClassId,
                item.SchoolId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (student is null)
        {
            return null;
        }

        var schoolClass = student.ClassId.HasValue
            ? await dbContext.Classes.AsNoTracking()
                .Where(item => item.Id == student.ClassId.Value)
                .Select(item => new { item.Name, item.GradeLevel })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var subjects = student.ClassId.HasValue
            ? await dbContext.TeachingAssignments.AsNoTracking()
                .Where(item => item.ClassId == student.ClassId.Value)
                .Select(item => item.SubjectName)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(cancellationToken)
            : [];

        return new StudentDashboard(
            student.LocalId,
            student.FirstName,
            student.LastName,
            schoolClass?.Name,
            schoolClass?.GradeLevel,
            subjects);
    }

    /// <summary>Dashboard counts for a school administrator.</summary>
    public sealed record PrincipalDashboard(int StudentCount, int TeacherCount, int ClassCount, int AssignmentCount);

    /// <summary>School-wide counts and data-backed monthly and class-grouped student records.</summary>
    public sealed record PrincipalAnalytics(
        int Year,
        int CurrentStudentCount,
        int TeacherCount,
        int ClassCount,
        int AssignmentCount,
        IReadOnlyList<MonthlyEnrollment> MonthlyEnrollments,
        IReadOnlyList<ClassEnrollment> StudentsByClass,
        string EnrollmentLimitation);

    /// <summary>Current student records created in one calendar month.</summary>
    public sealed record MonthlyEnrollment(int Month, string Label, int Count);

    /// <summary>Current student records grouped by their assigned class.</summary>
    public sealed record ClassEnrollment(string Label, int Count);

    /// <summary>Student count for one teacher-assigned class, without duplicating subject assignments.</summary>
    public sealed record ClassSize(string Label, int StudentCount);

    /// <summary>A teacher's assigned class and subject with a tenant-scoped student count.</summary>
    public sealed record TeacherClass(Guid Id, string Name, string GradeLevel, string SubjectName, int StudentCount);

    /// <summary>A teacher's identity and only the class assignments they may access.</summary>
    public sealed record TeacherDashboard(
        string Name,
        IReadOnlyList<TeacherClass> Classes,
        int AssignedClassCount,
        int AssignedSubjectCount,
        int AssignedStudentCount,
        IReadOnlyList<ClassSize> StudentsByClass);

    /// <summary>Student information exposed to an assigned teacher for class instruction.</summary>
    public sealed record StudentSummary(Guid Id, string LocalId, string FirstName, string LastName);

    /// <summary>Profile and course-subject information linked to one student's identity.</summary>
    public sealed record StudentDashboard(
        string LocalId,
        string FirstName,
        string LastName,
        string? ClassName,
        string? GradeLevel,
        IReadOnlyList<string> Subjects);
}
