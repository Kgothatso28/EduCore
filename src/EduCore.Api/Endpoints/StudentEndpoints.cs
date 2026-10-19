using EduCore.Api.Domain;
using EduCore.Api.Services;

namespace EduCore.Api.Endpoints;

/// <summary>Defines tenant-scoped HTTP routes for student administration.</summary>
public static class StudentEndpoints
{
    /// <summary>Maps student CRUD/search routes behind the supplied school authorization policy.</summary>
    public static IEndpointRouteBuilder MapStudentEndpoints(
        this IEndpointRouteBuilder endpoints,
        string authorizationPolicy)
    {
        var group = endpoints.MapGroup("/api/v1/students")
            .RequireAuthorization(authorizationPolicy);

        // Search and paginate within the school enforced by the tenant-aware data context.
        group.MapGet("/", async (
            string? search,
            int? page,
            int? pageSize,
            StudentService students,
            CancellationToken cancellationToken) =>
        {
            var results = await students.SearchAsync(
                search, page ?? 1, pageSize ?? 10, cancellationToken);
            return Results.Ok(new StudentPageResponse(
                results.Items.Select(StudentResponse.From).ToArray(),
                results.Page, results.PageSize, results.TotalCount, results.TotalPages));
        });

        // Accept only student fields; the server derives SchoolId from validated request context.
        group.MapPost("/", async (
            StudentWriteRequest request,
            StudentService students,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var student = await students.CreateAsync(
                    request.LocalId, request.FirstName, request.LastName, cancellationToken);
                return Results.Created($"/api/v1/students/{student.Id}", StudentResponse.From(student));
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["student"] = [exception.Message]
                });
            }
        });

        // A foreign-tenant identifier appears absent because the query filter hides that row.
        group.MapGet("/{id:guid}", async (
            Guid id,
            StudentService students,
            CancellationToken cancellationToken) =>
        {
            var student = await students.FindAsync(id, cancellationToken);
            return student is null ? Results.NotFound() : Results.Ok(StudentResponse.From(student));
        });

        // Update only a row visible in the active tenant and preserve its existing ownership.
        group.MapPut("/{id:guid}", async (
            Guid id,
            StudentWriteRequest request,
            StudentService students,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var updated = await students.UpdateAsync(
                    id, request.LocalId, request.FirstName, request.LastName, cancellationToken);
                return updated ? Results.NoContent() : Results.NotFound();
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["student"] = [exception.Message]
                });
            }
        });

        // Delete only a row visible through the active tenant's query filter.
        group.MapDelete("/{id:guid}", async (
            Guid id,
            StudentService students,
            CancellationToken cancellationToken) =>
                await students.DeleteAsync(id, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound());

        return endpoints;
    }

    /// <summary>Contains client-editable student fields, intentionally excluding tenant ownership.</summary>
    public sealed record StudentWriteRequest(string LocalId, string FirstName, string LastName);

    /// <summary>Projects a persisted student into the public API response shape.</summary>
    public sealed record StudentResponse(Guid Id, Guid SchoolId, string LocalId, string FirstName, string LastName)
    {
        /// <summary>Copies the allowed student fields into a response object.</summary>
        public static StudentResponse From(Student student) =>
            new(student.Id, student.SchoolId, student.LocalId, student.FirstName, student.LastName);
    }

    /// <summary>Returns one page of students and the metadata needed for navigation.</summary>
    public sealed record StudentPageResponse(
        IReadOnlyList<StudentResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
