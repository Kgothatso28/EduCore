using Microsoft.AspNetCore.Mvc;
using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/classes")]
public class ClassesController : ControllerBase
{
    private readonly IClassRepository _classes;

    public ClassesController(IClassRepository classes)
    {
        _classes = classes;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var list = await _classes.GetAllAsync(string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Query["tenant"].ToString();
        var item = await _classes.GetByIdAsync(id, string.IsNullOrEmpty(tenant) ? null : tenant, cancellationToken);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ClassDto dto, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenant)) dto.TenantId = tenant;
        var created = await _classes.AddAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var tenant = User.FindFirst("tenant_id")?.Value;
        var ok = await _classes.DeleteAsync(id, tenant, cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
