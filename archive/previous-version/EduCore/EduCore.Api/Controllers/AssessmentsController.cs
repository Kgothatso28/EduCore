using Microsoft.AspNetCore.Mvc;
using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/assessments")]
public class AssessmentsController : ControllerBase
{
    private readonly IAssessmentRepository _repo;

    public AssessmentsController(IAssessmentRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var list = await _repo.GetAllAsync(string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var item = await _repo.GetByIdAsync(id, string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AssessmentDto dto, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenant)) dto.TenantId = tenant;
        var created = await _repo.AddAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id}/marks")]
    public async Task<IActionResult> AddMark(Guid id, [FromBody] AssessmentMarkDto mark, CancellationToken cancellationToken)
    {
        mark.AssessmentId = id;
        var tenant = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenant)) mark.TenantId = tenant;
        var created = await _repo.AddMarkAsync(mark, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = mark.AssessmentId }, created);
    }
}
