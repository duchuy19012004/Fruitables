using System.Reflection;
using Fruitables.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Fruitables.Tests;

public class MigrationGuardTests
{
    [Fact]
    public void HardenPriceProduction_rejects_invalid_base_prices_and_schedule_ranges()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(HardenPriceProduction).GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic);

        up!.Invoke(new HardenPriceProduction(), [builder]);

        var sql = string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("FROM Products", sql);
        Assert.Contains("ProductVariants", sql);
        Assert.Contains("Price <> FLOOR", sql);
        Assert.Contains("schedule.EndsAt <= schedule.StartsAt", sql);
    }
}
