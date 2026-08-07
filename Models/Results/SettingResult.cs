namespace Fruitables.Models
{
    /// <summary>
    /// Result object for setting operations
    /// </summary>
    public class SettingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Value { get; set; }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static SettingResult Ok(string? value = null) => new()
        {
            Success = true,
            Value = value
        };

        /// <summary>
        /// Creates an error result with the specified message
        /// </summary>
        public static SettingResult Error(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
