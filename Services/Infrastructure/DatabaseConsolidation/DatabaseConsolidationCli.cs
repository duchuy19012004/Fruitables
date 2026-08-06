namespace Fruitables.Services.Infrastructure.DatabaseConsolidation;

public static class DatabaseConsolidationCli
{
    public static int ExitCode(bool success) => success ? 0 : 1;
}
