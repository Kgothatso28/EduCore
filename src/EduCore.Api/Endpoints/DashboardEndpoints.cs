using System.Security.Claims;
using EduCore.Api.Services;

namespace EduCore.Api.Endpoints;

/// <summary>Maps role-protected dashboard APIs for school administrators, teachers, and students.</summary>
public static class DashboardEndpoints
{
    /// <summary>Registers dashboards whose data access is independently scoped by server authorization.</summary>
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/dashboard/principal", async (
            DashboardService dashboards,
            CancellationToken cancellationToken) =>
        {
            var result = await dashboards.GetPrincipalAsync(cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("SchoolAdministrator");

        endpoints.MapGet("/api/v1/dashboard/principal/analytics", async (
            int? year,
            DashboardService dashboards,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await dashboards.GetPrincipalAnalyticsAsync(
                    year ?? DateTime.UtcNow.Year, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["year"] = [$"Year must be between 1 and {DateTime.UtcNow.Year}."]
                });
            }
        }).RequireAuthorization("SchoolAdministrator");

        endpoints.MapGet("/api/v1/dashboard/teacher", async (
            ClaimsPrincipal user,
            DashboardService dashboards,
            CancellationToken cancellationToken) =>
        {
            var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (subjectId is null)
            {
                return Results.Unauthorized();
            }

            var result = await dashboards.GetTeacherAsync(subjectId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("SchoolTeacher");

        endpoints.MapGet("/api/v1/dashboard/teacher/classes/{classId:guid}/students", async (
            Guid classId,
            ClaimsPrincipal user,
            DashboardService dashboards,
            CancellationToken cancellationToken) =>
        {
            var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (subjectId is null)
            {
                return Results.Unauthorized();
            }

            var result = await dashboards.GetTeacherRosterAsync(subjectId, classId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("SchoolTeacher");

        endpoints.MapGet("/api/v1/dashboard/student", async (
            ClaimsPrincipal user,
            DashboardService dashboards,
            CancellationToken cancellationToken) =>
        {
            var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (subjectId is null)
            {
                return Results.Unauthorized();
            }

            var result = await dashboards.GetStudentAsync(subjectId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("SchoolStudent");

        return endpoints;
    }
}
