using Microsoft.AspNetCore.Mvc;
using EduCore.Application.DTOs;
using EduCore.Application.Interfaces;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/examinations")]
public class ExaminationsController : ControllerBase
{
    private readonly IExaminationRepository _repo;
    public ExaminationsController(IExaminationRepository repo)
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ExaminationDto dto, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenant)) dto.TenantId = tenant;
        var created = await _repo.AddAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }
}
