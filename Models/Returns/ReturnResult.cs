namespace Fruitables.Models.Returns;

public record ReturnResult(bool Success, string? Error, ReturnRequest? Request = null, bool IsConcurrencyConflict = false)
{
    public static ReturnResult Fail(string error, bool conflict = false) => new(false, error, null, conflict);
    public static ReturnResult Ok(ReturnRequest request) => new(true, null, request);
}
