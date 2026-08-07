namespace Fruitables.Models;

public class ComboResult
{
    public bool Success { get; private set; }
    public Combo? Combo { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ComboResult() { }

    public static ComboResult Ok(Combo? combo = null)
        => new() { Success = true, Combo = combo };

    public static ComboResult Fail(string message)
        => new() { Success = false, ErrorMessage = message };
}
