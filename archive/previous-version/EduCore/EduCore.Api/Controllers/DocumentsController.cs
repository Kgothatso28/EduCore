using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentRepository _repo;

    public DocumentsController(IDocumentRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var docs = await _repo.GetAllAsync(tenant);
        return Ok(docs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var d = await _repo.GetByIdAsync(id, tenant);
        if (d == null) return NotFound();
        return Ok(d);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentDto doc)
    {
        doc.TenantId ??= User.FindFirst("tenant_id")?.Value ?? doc.TenantId;
        var created = await _repo.AddAsync(doc);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _repo.DeleteAsync(id);
        return NoContent();
    }
}
