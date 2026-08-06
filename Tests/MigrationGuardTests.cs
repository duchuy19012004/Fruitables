using Xunit;

namespace Fruitables.Tests;

public sealed class MigrationGuardTests
{
    [Fact]
    public void AddAggregateJsonSchema_is_additive_and_does_not_drop_legacy_tables()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migrationFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot, "Migrations"),
            "*_AddAggregateJsonSchema.cs");

        var migrationFile = Assert.Single(migrationFiles);
        var source = File.ReadAllText(migrationFile);
        var upMethod = source[..source.IndexOf("protected override void Down", StringComparison.Ordinal)];

        Assert.DoesNotContain("DropTable(", upMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn(", upMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("RenameTable(", upMethod, StringComparison.Ordinal);
        Assert.Contains("CreateTable(", upMethod, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fruitables.csproj")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
