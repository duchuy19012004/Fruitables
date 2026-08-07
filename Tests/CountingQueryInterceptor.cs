using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fruitables.Tests;

// Đếm SELECT theo bảng/pattern để làm guardrail chống N+1. Pattern khớp substring
// không phân biệt hoa/thường; bỏ qua query hệ thống (INFORMATION_SCHEMA, sqlite_master).
public class CountingQueryInterceptor : DbCommandInterceptor
{
    private int _totalSelectCount;
    private readonly Dictionary<string, int> _tableCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _countsLock = new();
    private readonly List<string> _selectCommands = new();

    public int TotalSelectCount => _totalSelectCount;

    // Đếm SELECT trên bảng Products. Giữ lại cho test baseline của checkout.
    public int ProductSelectCount => GetCount("Products");
    public IReadOnlyList<string> SelectCommands
    {
        get
        {
            lock (_countsLock)
                return _selectCommands.ToList();
        }
    }

    public CountingQueryInterceptor()
    {
        Register("Products");
    }

    // Đăng ký thêm pattern bảng cần đếm. Idempotent: đăng ký trùng không nhân đôi.
    public void Register(string tablePattern)
    {
        lock (_countsLock)
        {
            if (!_tableCounts.ContainsKey(tablePattern))
            {
                _tableCounts[tablePattern] = 0;
            }
        }
    }

    // Số SELECT khớp với pattern đã đăng ký. Auto-register pattern chưa có nhưng giá trị
    // chỉ bắt đầu được đếm từ thời điểm đăng ký — gọi Register trước khi chạy query để đếm đúng.
    public int GetCount(string tablePattern)
    {
        lock (_countsLock)
        {
            if (!_tableCounts.ContainsKey(tablePattern))
            {
                _tableCounts[tablePattern] = 0;
            }
            return _tableCounts[tablePattern];
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        CountQuery(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        CountQuery(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        CountQuery(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        CountQuery(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void CountQuery(DbCommand command)
    {
        var text = command.CommandText;
        if (!text.Contains("SELECT", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (text.Contains("INFORMATION_SCHEMA", System.StringComparison.OrdinalIgnoreCase) ||
            text.Contains("sqlite_master", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Interlocked.Increment(ref _totalSelectCount);

        lock (_countsLock)
        {
            _selectCommands.Add(text);
            foreach (var pattern in _tableCounts.Keys.ToList())
            {
                if (text.Contains(pattern, System.StringComparison.OrdinalIgnoreCase))
                {
                    _tableCounts[pattern]++;
                }
            }
        }
    }
}
