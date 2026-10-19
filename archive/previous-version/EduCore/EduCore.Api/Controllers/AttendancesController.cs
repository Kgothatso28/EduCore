using Microsoft.AspNetCore.Mvc;
using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/attendances")]
public class AttendancesController : ControllerBase
{
    private readonly IAttendanceRepository _attendance;

    public AttendancesController(IAttendanceRepository attendance)
    {
        _attendance = attendance;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var list = await _attendance.GetAllAsync(string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        return Ok(list);
    }

    [HttpGet("bystudent/{studentId}")]
    public async Task<IActionResult> GetByStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var list = await _attendance.GetByStudentAsync(studentId, string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        return Ok(list);
    }

    [HttpGet("byclass/{classId}")]
    public async Task<IActionResult> GetByClass(Guid classId, [FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var list = await _attendance.GetByClassAsync(classId, date, string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        return Ok(list);
    }

    [HttpGet("bydate")]
    public async Task<IActionResult> GetByDate([FromQuery] DateTime date, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var list = await _attendance.GetByDateAsync(date, string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] AttendanceDto dto, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenant)) dto.TenantId = tenant;
        var created = await _attendance.RecordAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetByStudent), new { studentId = created.StudentId }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AttendanceDto dto, CancellationToken cancellationToken)
    {
        dto.Id = id;
        var updated = await _attendance.UpdateAsync(dto, cancellationToken);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var summary = await _attendance.GetSummaryAsync(start, end, string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        return Ok(summary);
    }
}
