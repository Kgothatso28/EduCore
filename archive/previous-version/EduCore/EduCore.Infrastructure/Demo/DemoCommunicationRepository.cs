using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoCommunicationRepository : ICommunicationRepository
{
    private readonly List<AnnouncementDto> _announcements = new();
    private readonly List<MessageDto> _messages = new();

    public DemoCommunicationRepository()
    {
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        _announcements.Add(new AnnouncementDto { Id = Guid.NewGuid(), Title = "Welcome Back", Body = "Welcome to Term 1!", Priority = "Normal", Date = DateTime.UtcNow.AddDays(-10), TenantId = tenantA });
        _announcements.Add(new AnnouncementDto { Id = Guid.NewGuid(), Title = "Exam Schedule", Body = "See upcoming exams.", Priority = "Important", Date = DateTime.UtcNow.AddDays(-3), TenantId = tenantB });

        _messages.Add(new MessageDto { Id = Guid.NewGuid(), FromUserId = Guid.NewGuid(), ToUserId = Guid.NewGuid(), Subject = "Fee Reminder", Body = "Your fee is due.", Read = false, Date = DateTime.UtcNow.AddDays(-2), TenantId = tenantA });
    }

    public Task<AnnouncementDto> AddAnnouncementAsync(AnnouncementDto announcement, CancellationToken cancellationToken = default)
    {
        announcement.Id = Guid.NewGuid();
        _announcements.Add(announcement);
        return Task.FromResult(announcement);
    }

    public Task<IEnumerable<AnnouncementDto>> GetAnnouncementsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _announcements.AsEnumerable();
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(a => a.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<MessageDto> SendMessageAsync(MessageDto message, CancellationToken cancellationToken = default)
    {
        message.Id = Guid.NewGuid();
        _messages.Add(message);
        return Task.FromResult(message);
    }

    public Task<IEnumerable<MessageDto>> GetMessagesForUserAsync(Guid userId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _messages.Where(m => m.ToUserId == userId);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(m => m.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task MarkMessageReadAsync(Guid messageId)
    {
        var m = _messages.FirstOrDefault(x => x.Id == messageId);
        if (m != null) m.Read = true;
        return Task.CompletedTask;
    }
}
