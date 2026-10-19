using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CommunicationsController : ControllerBase
{
    private readonly ICommunicationRepository _repo;

    public CommunicationsController(ICommunicationRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("announcements")]
    public async Task<IActionResult> Announcements([FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var a = await _repo.GetAnnouncementsAsync(tenant);
        return Ok(a);
    }

    [HttpPost("announcements")]
    public async Task<IActionResult> CreateAnnouncement([FromBody] AnnouncementDto announcement)
    {
        announcement.TenantId ??= User.FindFirst("tenant_id")?.Value ?? announcement.TenantId;
        var created = await _repo.AddAnnouncementAsync(announcement);
        return CreatedAtAction(nameof(Announcements), new { id = created.Id }, created);
    }

    [HttpGet("messages/{userId}")]
    public async Task<IActionResult> Messages(Guid userId, [FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var msgs = await _repo.GetMessagesForUserAsync(userId, tenant);
        return Ok(msgs);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] MessageDto message)
    {
        message.TenantId ??= User.FindFirst("tenant_id")?.Value ?? message.TenantId;
        var created = await _repo.SendMessageAsync(message);
        return CreatedAtAction(nameof(Messages), new { userId = created.ToUserId }, created);
    }

    [HttpPost("messages/{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _repo.MarkMessageReadAsync(id);
        return NoContent();
    }
}
