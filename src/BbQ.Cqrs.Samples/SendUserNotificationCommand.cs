using BbQ.Cqrs;

namespace BbQ.CQRS.Samples;

/// <summary>
/// A fire-and-forget command that sends a notification to a user.
/// 
/// This command demonstrates the IRequest pattern (without TResponse)
/// for operations that don't need to return a meaningful value.
/// </summary>
[Command]
public class SendUserNotificationCommand : IRequest
{
    /// <summary>
    /// The ID of the user to notify
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// The notification message to send
    /// </summary>
    public string Message { get; set; }

    public SendUserNotificationCommand(string userId, string message)
    {
        UserId = userId;
        Message = message;
    }
}
