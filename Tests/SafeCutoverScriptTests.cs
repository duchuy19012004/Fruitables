using Xunit;

namespace Fruitables.Tests;

public sealed class SafeCutoverScriptTests
{
    [Fact]
    public void Safe_cutover_script_requires_preflight_backup_and_explicit_contract_confirmation()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "database-consolidation-safe-cutover.ps1"));

        Assert.Contains("[string]$Phase", script, StringComparison.Ordinal);
        Assert.Contains("BACKUP DATABASE", script, StringComparison.Ordinal);
        Assert.Contains("RESTORE VERIFYONLY", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-Preflight", script, StringComparison.Ordinal);
        Assert.Contains("-ConfirmContract", script, StringComparison.Ordinal);
        Assert.Contains("-ExpectedDatabaseName", script, StringComparison.Ordinal);
        Assert.Contains("ContractAggregateSchema", script, StringComparison.Ordinal);
        Assert.Contains("Missing target column", script, StringComparison.Ordinal);
        Assert.Contains("Unexpected business table", script, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fruitables.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
