namespace Fruitables.Services.Communications;

/// <summary>
/// Service interface for sending email notifications
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an account lock notification email to the customer
    /// </summary>
    /// <param name="customerEmail">Customer's email address</param>
    /// <param name="customerName">Customer's name</param>
    /// <param name="violationType">Type of violation</param>
    /// <param name="reason">Detailed reason for the lock</param>
    /// <param name="lockType">Type of lock (Temporary/Permanent)</param>
    /// <param name="expiresAt">Lock expiration date (null for permanent)</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendAccountLockedEmailAsync(
        string customerEmail,
        string customerName,
        string violationType,
        string reason,
        string lockType,
        DateTime? expiresAt);

    /// <summary>
    /// Sends an account unlock notification email to the customer
    /// </summary>
    /// <param name="customerEmail">Customer's email address</param>
    /// <param name="customerName">Customer's name</param>
    /// <param name="reason">Reason for unlocking</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendAccountUnlockedEmailAsync(
        string customerEmail,
        string customerName,
        string reason);

    /// <summary>
    /// Sends a password reset link email to the user
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="resetLink">The full URL link to reset password</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendPasswordResetEmailAsync(string email, string resetLink);

    /// <summary>
    /// Sends a customer care follow-up email (ví dụ: hỏi thăm sau review tiêu cực)
    /// </summary>
    Task<bool> SendFollowUpEmailAsync(
        string customerEmail,
        string customerName,
        string subject,
        string message);
}
