namespace Chronos.Core.Abstractions.Strategies;

/// <summary>Contract for sending out‑of‑band notifications (email, Telegram, etc.).</summary>
public interface INotificationChannel
{
    /// <summary>Sends a message asynchronously.</summary>
    Task SendAsync(string message);
}
