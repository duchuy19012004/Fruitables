using Fruitables.Constants;
using Fruitables.Services.Communications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Fruitables.Services.Infrastructure;

namespace Fruitables.Services.Communications;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly ISettingsService _settingsService;

    private const string SUPPORT_EMAIL = "support@fruitables.com";
    private const string COMPANY_NAME = "Fruitables";

    public EmailService(ILogger<EmailService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async Task<bool> SendAccountLockedEmailAsync(
        string customerEmail,
        string customerName,
        string violationType,
        string reason,
        string lockType,
        DateTime? expiresAt)
    {
        try
        {
            var subject = $"[{COMPANY_NAME}] Thông báo khóa tài khoản";
            var body = GenerateAccountLockedEmailBody(customerName, violationType, reason, lockType, expiresAt);
            return await SendEmailAsync(customerEmail, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send account locked email to {Email}", customerEmail);
            return false;
        }
    }

    public async Task<bool> SendAccountUnlockedEmailAsync(
        string customerEmail,
        string customerName,
        string reason)
    {
        try
        {
            var subject = $"[{COMPANY_NAME}] Thông báo mở khóa tài khoản";
            var body = GenerateAccountUnlockedEmailBody(customerName, reason);
            return await SendEmailAsync(customerEmail, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send account unlocked email to {Email}", customerEmail);
            return false;
        }
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
    {
        try
        {
            var subject = $"[{COMPANY_NAME}] Đặt lại mật khẩu";
            var body = GeneratePasswordResetEmailBody(resetLink);
            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendFollowUpEmailAsync(
        string customerEmail,
        string customerName,
        string subject,
        string message)
    {
        try
        {
            var body = GenerateFollowUpEmailBody(customerName, message);
            return await SendEmailAsync(customerEmail, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send follow-up email to {Email}", customerEmail);
            return false;
        }
    }

    // Gửi email qua SMTP (MailKit), cấu hình đọc từ DB thông qua ISettingsService
    private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var host = await _settingsService.GetSettingAsync(SettingKeys.SmtpHost);
        var portStr = await _settingsService.GetSettingAsync(SettingKeys.SmtpPort);
        var username = await _settingsService.GetSettingAsync(SettingKeys.SmtpUsername);
        var password = await _settingsService.GetSettingAsync(SettingKeys.SmtpPassword);
        var enableSslStr = await _settingsService.GetSettingAsync(SettingKeys.SmtpEnableSsl);
        var senderName = await _settingsService.GetSettingAsync(SettingKeys.SmtpSenderName) ?? COMPANY_NAME;
        var senderEmail = await _settingsService.GetSettingAsync(SettingKeys.SmtpSenderEmail);

        // Nếu chưa cấu hình SMTP thì bỏ qua, log để debug
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("SMTP chưa được cấu hình. Email tới {ToEmail} không được gửi.", toEmail);
            _logger.LogInformation("[DEV] Subject: {Subject} | To: {To}", subject, toEmail);
            return true;
        }

        int port = int.TryParse(portStr, out var p) ? p : 587;
        bool enableSsl = !bool.TryParse(enableSslStr, out var ssl) || ssl;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail ?? username));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        using var client = new SmtpClient();
        var secureOption = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(host, port, secureOption);
        await client.AuthenticateAsync(username, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email gửi thành công tới {ToEmail}", toEmail);
        return true;
    }

    // Template HTML email thông báo khóa tài khoản
    private string GenerateAccountLockedEmailBody(
        string customerName,
        string violationType,
        string reason,
        string lockType,
        DateTime? expiresAt)
    {
        var lockTypeText = lockType == "Temporary" ? "tạm thời" : "vĩnh viễn";
        var expirationText = expiresAt.HasValue
            ? $"<p><strong>Thời gian hết hạn:</strong> {expiresAt.Value:dd/MM/yyyy HH:mm}</p>"
            : "<p><strong>Thời gian hết hạn:</strong> Không xác định (khóa vĩnh viễn)</p>";

        return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Thông báo khóa tài khoản</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border: 1px solid #dee2e6; }}
        .info-box {{ background-color: #fff; border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0; }}
        .appeal-section {{ background-color: #e7f3ff; border-left: 4px solid #0d6efd; padding: 15px; margin: 20px 0; }}
        .footer {{ background-color: #343a40; color: #adb5bd; padding: 20px; text-align: center; font-size: 12px; border-radius: 0 0 8px 8px; }}
        h1 {{ margin: 0; font-size: 24px; }}
        h2 {{ color: #dc3545; font-size: 18px; }}
        .highlight {{ color: #dc3545; font-weight: bold; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>⚠️ Thông báo khóa tài khoản</h1>
    </div>

    <div class=""content"">
        <p>Xin chào <strong>{customerName}</strong>,</p>
        <p>Chúng tôi rất tiếc phải thông báo rằng tài khoản của bạn tại <strong>{COMPANY_NAME}</strong>
        đã bị <span class=""highlight"">khóa {lockTypeText}</span>.</p>

        <div class=""info-box"">
            <h2>📋 Chi tiết khóa tài khoản</h2>
            <p><strong>Loại vi phạm:</strong> {violationType}</p>
            <p><strong>Lý do chi tiết:</strong></p>
            <p style=""padding-left: 15px; border-left: 2px solid #ccc;"">{reason}</p>
            <p><strong>Loại khóa:</strong> {(lockType == "Temporary" ? "Tạm thời" : "Vĩnh viễn")}</p>
            {expirationText}
        </div>

        <div class=""appeal-section"">
            <h2>📝 Hướng dẫn khiếu nại</h2>
            <p>Nếu bạn cho rằng đây là một sự nhầm lẫn hoặc muốn khiếu nại quyết định này,
            vui lòng liên hệ với chúng tôi qua:</p>
            <ul>
                <li><strong>Email:</strong> <a href=""mailto:{SUPPORT_EMAIL}"">{SUPPORT_EMAIL}</a></li>
                <li><strong>Tiêu đề email:</strong> [Khiếu nại] Yêu cầu xem xét khóa tài khoản - {customerName}</li>
            </ul>
            <p>Trong email khiếu nại, vui lòng cung cấp:</p>
            <ol>
                <li>Họ tên đầy đủ và email đăng ký</li>
                <li>Lý do bạn cho rằng quyết định khóa là không chính xác</li>
                <li>Bất kỳ bằng chứng hoặc thông tin bổ sung nào</li>
            </ol>
            <p><em>Chúng tôi sẽ xem xét và phản hồi trong vòng 3-5 ngày làm việc.</em></p>
        </div>

        <p>Chúng tôi hiểu rằng đây có thể là tin không vui, nhưng chúng tôi cam kết
        duy trì một môi trường an toàn và công bằng cho tất cả khách hàng.</p>

        <p>Trân trọng,<br><strong>Đội ngũ {COMPANY_NAME}</strong></p>
    </div>

    <div class=""footer"">
        <p>© {DateTime.Now.Year} {COMPANY_NAME}. Tất cả quyền được bảo lưu.</p>
        <p>Email này được gửi tự động, vui lòng không trả lời trực tiếp.</p>
        <p>Nếu bạn cần hỗ trợ, hãy liên hệ: {SUPPORT_EMAIL}</p>
    </div>
</body>
</html>";
    }

    // Template HTML email thông báo mở khóa tài khoản
    private string GenerateAccountUnlockedEmailBody(string customerName, string reason)
    {
        return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Thông báo mở khóa tài khoản</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border: 1px solid #dee2e6; }}
        .info-box {{ background-color: #fff; border-left: 4px solid #28a745; padding: 15px; margin: 20px 0; }}
        .cta-section {{ text-align: center; margin: 30px 0; }}
        .cta-button {{ display: inline-block; background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; }}
        .footer {{ background-color: #343a40; color: #adb5bd; padding: 20px; text-align: center; font-size: 12px; border-radius: 0 0 8px 8px; }}
        h1 {{ margin: 0; font-size: 24px; }}
        h2 {{ color: #28a745; font-size: 18px; }}
        .highlight {{ color: #28a745; font-weight: bold; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>✅ Thông báo mở khóa tài khoản</h1>
    </div>

    <div class=""content"">
        <p>Xin chào <strong>{customerName}</strong>,</p>
        <p>Chúng tôi vui mừng thông báo rằng tài khoản của bạn tại <strong>{COMPANY_NAME}</strong>
        đã được <span class=""highlight"">mở khóa thành công</span>!</p>

        <div class=""info-box"">
            <h2>📋 Chi tiết mở khóa</h2>
            <p><strong>Lý do mở khóa:</strong></p>
            <p style=""padding-left: 15px; border-left: 2px solid #ccc;"">{reason}</p>
            <p><strong>Thời gian mở khóa:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
        </div>

        <p>Bạn có thể đăng nhập và sử dụng tất cả các dịch vụ của {COMPANY_NAME} như bình thường.</p>

        <div class=""cta-section"">
            <a href=""#"" class=""cta-button"">Đăng nhập ngay</a>
        </div>

        <div class=""info-box"" style=""border-left-color: #ffc107;"">
            <h2 style=""color: #856404;"">⚠️ Lưu ý quan trọng</h2>
            <p>Để tránh việc tài khoản bị khóa trong tương lai, vui lòng:</p>
            <ul>
                <li>Tuân thủ <a href=""#"">Điều khoản sử dụng</a> của {COMPANY_NAME}</li>
                <li>Không thực hiện các hành vi vi phạm chính sách</li>
                <li>Liên hệ hỗ trợ nếu có bất kỳ thắc mắc nào</li>
            </ul>
        </div>

        <p>Cảm ơn bạn đã là khách hàng của {COMPANY_NAME}. Chúng tôi rất vui được phục vụ bạn!</p>

        <p>Trân trọng,<br><strong>Đội ngũ {COMPANY_NAME}</strong></p>
    </div>

    <div class=""footer"">
        <p>© {DateTime.Now.Year} {COMPANY_NAME}. Tất cả quyền được bảo lưu.</p>
        <p>Email này được gửi tự động, vui lòng không trả lời trực tiếp.</p>
        <p>Nếu bạn cần hỗ trợ, hãy liên hệ: {SUPPORT_EMAIL}</p>
    </div>
</body>
</html>";
    }

    // Template HTML email chăm sóc khách hàng (follow-up sau phản hồi tiêu cực)
    private string GenerateFollowUpEmailBody(string customerName, string message)
    {
        return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Thư từ Fruitables</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #81c408; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border: 1px solid #dee2e6; }}
        .footer {{ background-color: #343a40; color: #adb5bd; padding: 20px; text-align: center; font-size: 12px; border-radius: 0 0 8px 8px; }}
        h1 {{ margin: 0; font-size: 22px; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🌱 {COMPANY_NAME}</h1>
    </div>

    <div class=""content"">
        <p>Xin chào <strong>{customerName}</strong>,</p>
        <p>{message}</p>
        <p>Trân trọng,<br><strong>Đội ngũ {COMPANY_NAME}</strong></p>
    </div>

    <div class=""footer"">
        <p>© {DateTime.Now.Year} {COMPANY_NAME}. Tất cả quyền được bảo lưu.</p>
        <p>Nếu bạn cần hỗ trợ, hãy liên hệ: {SUPPORT_EMAIL}</p>
    </div>
</body>
</html>";
    }

    // Template HTML email đặt lại mật khẩu
    private string GeneratePasswordResetEmailBody(string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Đặt lại mật khẩu</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #198754; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border: 1px solid #dee2e6; }}
        .cta-section {{ text-align: center; margin: 30px 0; }}
        .cta-button {{ display: inline-block; background-color: #198754; color: white; padding: 14px 36px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px; }}
        .note-box {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
        .footer {{ background-color: #343a40; color: #adb5bd; padding: 20px; text-align: center; font-size: 12px; border-radius: 0 0 8px 8px; }}
        h1 {{ margin: 0; font-size: 22px; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🔐 Đặt lại mật khẩu</h1>
    </div>

    <div class=""content"">
        <p>Bạn vừa yêu cầu đặt lại mật khẩu tài khoản <strong>{COMPANY_NAME}</strong>.</p>
        <p>Nhấn vào nút bên dưới để tạo mật khẩu mới. Liên kết này sẽ <strong>hết hạn sau 15 phút</strong>.</p>

        <div class=""cta-section"">
            <a href=""{resetLink}"" class=""cta-button"">Đặt lại mật khẩu</a>
        </div>

        <div class=""note-box"">
            <strong>⚠️ Lưu ý bảo mật:</strong> Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này. Mật khẩu hiện tại của bạn sẽ không thay đổi.
        </div>

        <p>Hoặc copy đường link sau vào trình duyệt:<br>
        <small style=""word-break: break-all; color: #666;"">{resetLink}</small></p>

        <p>Trân trọng,<br><strong>Đội ngũ {COMPANY_NAME}</strong></p>
    </div>

    <div class=""footer"">
        <p>© {DateTime.Now.Year} {COMPANY_NAME}. Tất cả quyền được bảo lưu.</p>
        <p>Email này được gửi tự động, vui lòng không trả lời trực tiếp.</p>
        <p>Nếu bạn cần hỗ trợ, hãy liên hệ: {SUPPORT_EMAIL}</p>
    </div>
</body>
</html>";
    }
}
