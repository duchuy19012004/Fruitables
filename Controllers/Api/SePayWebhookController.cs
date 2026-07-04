using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fruitables.Controllers.Api;

[ApiController]
[Route("api/sepay/webhook")]
public class SePayWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly SePayOptions _options;
    private readonly ILogger<SePayWebhookController> _logger;

    public SePayWebhookController(ApplicationDbContext context, IOptions<SePayOptions> options, ILogger<SePayWebhookController> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        if (!IsValidSignature(rawBody))
            return Unauthorized(new { success = false, message = "Invalid signature" });

        SePayWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SePayWebhookPayload>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return BadRequest(new { success = false, message = "Invalid JSON" });
        }

        if (payload == null)
            return BadRequest(new { success = false, message = "Invalid payload" });

        if (await _context.SePayTransactions.AnyAsync(t => t.SePayTransactionId == payload.Id))
            return Ok(new { success = true });

        var code = payload.Code?.Trim().ToUpperInvariant();
        var order = !string.IsNullOrWhiteSpace(code)
            ? await _context.Orders.FirstOrDefaultAsync(o => o.PaymentCode == code)
            : null;

        var transaction = new SePayTransaction
        {
            SePayTransactionId = payload.Id,
            OrderId = order?.Id,
            PaymentCode = code,
            TransferAmount = payload.TransferAmount,
            ReferenceCode = payload.ReferenceCode,
            Payload = rawBody,
            CreatedAt = DateTime.UtcNow
        };

        if (!IsPayable(payload, code, order, out var message))
        {
            transaction.Status = SePayTransactionStatus.Ignored;
            transaction.Message = message;
            _context.SePayTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            _logger.LogWarning("Ignored SePay transaction {TransactionId}: {Message}", payload.Id, message);
            return Ok(new { success = true });
        }

        order!.PaymentStatus = PaymentStatus.Paid;
        transaction.Status = SePayTransactionStatus.Paid;
        transaction.Message = "Matched";
        _context.SePayTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    private bool IsValidSignature(string rawBody)
    {
        var signature = Request.Headers["X-SePay-Signature"].ToString();
        var timestampText = Request.Headers["X-SePay-Timestamp"].ToString();
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret) ||
            string.IsNullOrWhiteSpace(signature) ||
            !long.TryParse(timestampText, out var timestamp))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > 300)
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestampText + "." + rawBody));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private bool IsPayable(SePayWebhookPayload payload, string? code, Order? order, out string message)
    {
        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            message = "Not incoming transfer";
            return false;
        }

        if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(_options.PaymentCodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            message = "Missing or invalid payment code";
            return false;
        }
        
        if (order == null)
        {
            message = "Order not found";
            return false;
        }

        if (order.PaymentMethod != PaymentMethod.BankTransfer)
        {
            message = "Order is not bank transfer";
            return false;
        }

        if (order.PaymentStatus != PaymentStatus.Pending)
        {
            message = "Order is not pending payment";
            return false;
        }

        if (payload.TransferAmount != order.Total)
        {
            message = "Amount mismatch";
            return false;
        }

        message = "Matched";
        return true;
    }

    private sealed class SePayWebhookPayload
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? TransferType { get; set; }
        public decimal TransferAmount { get; set; }
        public string? ReferenceCode { get; set; }
    }
}
