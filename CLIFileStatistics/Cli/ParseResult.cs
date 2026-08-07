namespace CLIFileStatistics.Cli;

public sealed class ParseResult
{
    public bool Success { get; private init; }
    public bool HelpRequested { get; private init; }
    public bool VersionRequested { get; private init; }
    public string? Error { get; private init; }
    public CliOptions? Options { get; private init; }

    public static ParseResult Help() => new() { Success = true, HelpRequested = true };

    public static ParseResult Version() => new() { Success = true, VersionRequested = true };

    public static ParseResult Ok(CliOptions options) => new() { Success = true, Options = options };

    public static ParseResult Fail(string error) => new() { Error = error };
}
