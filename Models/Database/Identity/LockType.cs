namespace Fruitables.Models;

/// <summary>
/// Loại khóa tài khoản
/// </summary>
public enum LockType
{
    /// <summary>
    /// Khóa tạm thời (có thời hạn)
    /// </summary>
    Temporary,
    
    /// <summary>
    /// Khóa vĩnh viễn
    /// </summary>
    Permanent
}
