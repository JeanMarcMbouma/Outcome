namespace BbQ.CQRS.Samples;

/// <summary>
/// Service interface for sending user notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification to a user.
    /// </summary>
    /// <param name="userId">The ID of the user to notify</param>
    /// <param name="message">The notification message</param>
    /// <param name="ct">Cancellation token</param>
    Task SendAsync(string userId, string message, CancellationToken ct);
}

/// <summary>
/// Fake notification service for testing.
/// </summary>
internal class FakeNotificationService : INotificationService
{
    public Task SendAsync(string userId, string message, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
