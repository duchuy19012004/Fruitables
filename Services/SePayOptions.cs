namespace Fruitables.Services;

public class SePayOptions
{
    public string WebhookSecret { get; set; } = string.Empty;
    public string PaymentCodePrefix { get; set; } = "FTB";
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
}
