using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ResultsController : ControllerBase
{
    private readonly IResultRepository _results;

    public ResultsController(IResultRepository results)
    {
        _results = results;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var res = await _results.GetAllAsync(tenant);
        return Ok(res);
    }

    [HttpGet("bystudent/{studentId}")]
    public async Task<IActionResult> ByStudent(Guid studentId, [FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var res = await _results.GetResultsByStudentAsync(studentId, tenant);
        return Ok(res);
    }

    [HttpGet("byclass/{classId}")]
    public async Task<IActionResult> ByClass(Guid classId, [FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var res = await _results.GetResultsByClassAsync(classId, tenant);
        return Ok(res);
    }
}
