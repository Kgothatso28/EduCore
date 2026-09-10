using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface ICommunicationRepository
{
    Task<IEnumerable<AnnouncementDto>> GetAnnouncementsAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<MessageDto>> GetMessagesForUserAsync(Guid userId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<AnnouncementDto> AddAnnouncementAsync(AnnouncementDto announcement, CancellationToken cancellationToken = default);
    Task<MessageDto> SendMessageAsync(MessageDto message, CancellationToken cancellationToken = default);
    Task MarkMessageReadAsync(Guid messageId);
}
