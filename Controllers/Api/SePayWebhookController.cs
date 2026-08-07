using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure;
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

        var providerTransactionId = payload.Id.ToString();
        if (await _context.Payments.AnyAsync(payment =>
                payment.Provider == "SePay" && payment.ProviderTransactionId == providerTransactionId))
            return Ok(new { success = true });

        var code = payload.Code?.Trim().ToUpperInvariant();
        var order = !string.IsNullOrWhiteSpace(code)
            ? await _context.Orders.FirstOrDefaultAsync(o => o.PaymentCode == code)
            : null;

        var payment = new Payment
        {
            OrderId = order?.Id ?? 0,
            Provider = "SePay",
            ProviderTransactionId = providerTransactionId,
            Amount = payload.TransferAmount,
            PaymentCode = code,
            ReferenceCode = payload.ReferenceCode,
            Message = rawBody,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        if (!IsPayable(payload, code, order, out var message))
        {
            if (order == null)
            {
                // Keep ignored events without inventing an order FK.
                _logger.LogWarning("Ignored SePay transaction {TransactionId}: {Message}", payload.Id, message);
                return Ok(new { success = true });
            }

            payment.OrderId = order.Id;
            payment.Status = PaymentStatus.Pending;
            payment.ProviderEventStatus = PaymentProviderEventStatus.Ignored;
            payment.Message = message;
            _context.Payments.Add(payment);
            if (!await TrySaveChangesAsync(payload))
                return Ok(new { success = true });
            _logger.LogWarning("Ignored SePay transaction {TransactionId}: {Message}", payload.Id, message);
            return Ok(new { success = true });
        }

        order!.PaymentStatus = PaymentStatus.Paid;
        payment.OrderId = order.Id;
        payment.Status = PaymentStatus.Paid;
        payment.ProviderEventStatus = PaymentProviderEventStatus.Accepted;
        payment.PaidAtUtc = DateTime.UtcNow;
        payment.Message = "Matched";
        _context.Payments.Add(payment);
        if (!await TrySaveChangesAsync(payload))
            return Ok(new { success = true });

        return Ok(new { success = true });
    }

    private async Task<bool> TrySaveChangesAsync(SePayWebhookPayload payload)
    {
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicatePayment(ex))
        {
            _logger.LogInformation("Duplicate SePay transaction {TransactionId} received concurrently; returning success.", payload.Id);
            return false;
        }
    }

    private static bool IsDuplicatePayment(DbUpdateException ex)
    {
        const int uniqueIndexViolation = 2601;
        const int uniqueConstraintViolation = 2627;

        if (ex.InnerException is null || !ex.Entries.Any(e => e.Entity is Payment))
            return false;

        var innerType = ex.InnerException.GetType();
        if (innerType.FullName is "Microsoft.Data.SqlClient.SqlException" or "System.Data.SqlClient.SqlException")
        {
            var numberProperty = innerType.GetProperty("Number");
            if (numberProperty is not null)
            {
                var number = (int)numberProperty.GetValue(ex.InnerException)!;
                return number is uniqueIndexViolation or uniqueConstraintViolation;
            }
        }

        // SQLite / InMemory uniqueness surfaces as DbUpdateException without SQL numbers.
        return ex.InnerException.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || ex.InnerException.Message.Contains("unique", StringComparison.OrdinalIgnoreCase);
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
