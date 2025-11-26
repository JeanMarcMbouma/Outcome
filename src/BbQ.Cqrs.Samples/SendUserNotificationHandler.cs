using BbQ.Cqrs;

namespace BbQ.CQRS.Samples;

/// <summary>
/// Handler for the SendUserNotificationCommand.
/// 
/// Implements IRequestHandler&lt;TRequest&gt; (without TResponse) for fire-and-forget operations.
/// This handler doesn't need to return a meaningful value - it just performs the side effect
/// of sending a notification.
/// </summary>
public class SendUserNotificationHandler : IRequestHandler<SendUserNotificationCommand>
{
    private readonly INotificationService _notificationService;

    public SendUserNotificationHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Sends a notification to the user without returning a value.
    /// </summary>
    /// <remarks>
    /// Fire-and-forget operations like this are useful for:
    /// - Sending emails or notifications
    /// - Publishing events
    /// - Logging or audit trail updates
    /// - Background task queueing
    /// 
    /// The caller doesn't care about the result, just that the operation was initiated.
    /// </remarks>
    public async Task Handle(SendUserNotificationCommand request, CancellationToken ct)
    {
        await _notificationService.SendAsync(request.UserId, request.Message, ct);
        Console.WriteLine($"  [Service] Notification sent to user {request.UserId}");
    }
}
