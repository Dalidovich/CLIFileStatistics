using CLIFileStatistics.Scanning;

namespace CLIFileStatistics.Cli;

public sealed class CliOptions
{
    private readonly List<string> _driveLetters = new();
    private readonly List<string> _scanPaths = new();

    public IReadOnlyList<string> DriveLetters => _driveLetters;
    public bool DrivesSpecified { get; private set; }
    public IReadOnlyList<string> ScanPaths => _scanPaths;
    public bool PathsSpecified { get; private set; }
    public string? OutputPath { get; private set; }
    public int Threads { get; private set; } = Environment.ProcessorCount;
    public char Separator { get; private set; } = ',';

    public static ParseResult Parse(string[] args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h":
                case "--help":
                    return ParseResult.Help();

                case "--version":
                    return ParseResult.Version();

                case "-d":
                case "--drives":
                {
                    if (!TryTakeValue(args, ref i, out var drives))
                        return ParseResult.Fail($"Argument {arg} requires a value: comma-separated drive letters.");
                    var driveError = options.SetDrives(drives);
                    if (driveError is not null)
                        return ParseResult.Fail(driveError);
                    break;
                }

                case "-p":
                case "--path":
                {
                    if (!TryTakeValue(args, ref i, out var path))
                        return ParseResult.Fail($"Argument {arg} requires a value: a directory path.");
                    options.AddPath(path);
                    break;
                }

                case "-s":
                case "--separator":
                {
                    if (!TryTakeValue(args, ref i, out var separator))
                        return ParseResult.Fail($"Argument {arg} requires a value: ',' ';' or comma/semicolon.");
                    var separatorError = options.SetSeparator(separator);
                    if (separatorError is not null)
                        return ParseResult.Fail(separatorError);
                    break;
                }

                case "-o":
                case "--output":
                {
                    if (!TryTakeValue(args, ref i, out var output))
                        return ParseResult.Fail($"Argument {arg} requires a value: a path to the CSV file.");
                    options.OutputPath = output;
                    break;
                }

                case "-t":
                case "--threads":
                {
                    if (!TryTakeValue(args, ref i, out var threads))
                        return ParseResult.Fail($"Argument {arg} requires a value: the number of threads.");
                    var threadsError = options.ParseThreads(threads);
                    if (threadsError is not null)
                        return ParseResult.Fail(threadsError);
                    break;
                }

                default:
                    if (arg.StartsWith("--drives=", StringComparison.Ordinal))
                    {
                        var driveError = options.SetDrives(arg["--drives=".Length..]);
                        if (driveError is not null)
                            return ParseResult.Fail(driveError);
                        break;
                    }

                    if (arg.StartsWith("--path=", StringComparison.Ordinal))
                    {
                        options.AddPath(arg["--path=".Length..]);
                        break;
                    }

                    if (arg.StartsWith("--output=", StringComparison.Ordinal))
                    {
                        options.OutputPath = arg["--output=".Length..];
                        break;
                    }

                    if (arg.StartsWith("--separator=", StringComparison.Ordinal))
                    {
                        var separatorError = options.SetSeparator(arg["--separator=".Length..]);
                        if (separatorError is not null)
                            return ParseResult.Fail(separatorError);
                        break;
                    }

                    if (arg.StartsWith("--threads=", StringComparison.Ordinal))
                    {
                        var threadsError = options.ParseThreads(arg["--threads=".Length..]);
                        if (threadsError is not null)
                            return ParseResult.Fail(threadsError);
                        break;
                    }

                    return ParseResult.Fail($"Unknown argument: {arg}");
            }
        }

        if (options.DrivesSpecified && options.PathsSpecified)
            return ParseResult.Fail("Flags -d/--drives and -p/--path are mutually exclusive: specify only one.");

        return ParseResult.Ok(options);
    }

    public List<ScanRoot> ResolveDrives()
    {
        var letters = DrivesSpecified
            ? (IEnumerable<string>)_driveLetters
            : new[] { GetExeDriveLetter() };

        var result = new List<ScanRoot>();

        foreach (var letter in letters)
        {
            try
            {
                var drive = new DriveInfo(letter + @":\");
                if (drive.DriveType is DriveType.NoRootDirectory or DriveType.Unknown)
                {
                    Console.Error.WriteLine($"Drive {letter}: not found — skipped.");
                    continue;
                }
                if (!drive.IsReady)
                {
                    Console.Error.WriteLine($"Drive {letter}: not ready (no media) — skipped.");
                    continue;
                }
                result.Add(new ScanRoot(drive.Name, drive.Name.TrimEnd('\\')));
            }
            catch
            {
                Console.Error.WriteLine($"Drive {letter}: not found — skipped.");
            }
        }

        return result;
    }

    public List<ScanRoot> ResolvePaths()
    {
        var result = new List<ScanRoot>();

        foreach (var scanPath in _scanPaths)
        {
            try
            {
                var fullPath = Path.GetFullPath(scanPath);
                if (!Directory.Exists(fullPath))
                {
                    Console.Error.WriteLine($"Directory {scanPath}: not found — skipped.");
                    continue;
                }
                var disk = Path.GetPathRoot(fullPath)?.TrimEnd('\\') ?? "";
                result.Add(new ScanRoot(fullPath, disk));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Directory {scanPath}: invalid path — {ex.Message}");
            }
        }

        return result;
    }

    public string ResolveOutputPath()
    {
        var path = OutputPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, DefaultFileName());
        }
        else if (path.EndsWith('\\') || path.EndsWith('/') || Directory.Exists(path))
        {
            path = Path.Combine(path, DefaultFileName());
        }

        if (string.IsNullOrEmpty(Path.GetExtension(path)))
            path += ".csv";

        return path;
    }

    private string? ParseThreads(string value)
    {
        if (!int.TryParse(value, out var count) || count < 1 || count > 512)
            return $"Invalid number of threads: '{value}'. Expected an integer from 1 to 512.";
        Threads = count;
        return null;
    }

    private string? SetSeparator(string value)
    {
        var trimmed = value.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (lower == "comma")
        {
            Separator = ',';
            return null;
        }
        if (lower == "semicolon")
        {
            Separator = ';';
            return null;
        }
        if (trimmed.Length == 1)
        {
            Separator = trimmed[0];
            return null;
        }

        return $"Invalid separator: '{value}'. Use ',' ';' or the words comma/semicolon.";
    }

    private string? SetDrives(string value)
    {
        DrivesSpecified = true;

        var parts = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var letter = part.TrimEnd(':').Trim().ToUpperInvariant();
            if (letter.Length != 1 || !char.IsLetter(letter[0]))
                return $"Invalid drive letter: '{part}'. Use letters only, e.g. C,D,E.";
            if (!_driveLetters.Contains(letter))
                _driveLetters.Add(letter);
        }

        if (_driveLetters.Count == 0)
            return "No drive letters specified.";

        return null;
    }

    private void AddPath(string value)
    {
        PathsSpecified = true;

        var parts = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;
            if (!_scanPaths.Contains(part))
                _scanPaths.Add(part);
        }
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = "";
            return false;
        }
        value = args[++index];
        return true;
    }

    public static string GetExeDriveLetter()
    {
        var root = Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\";
        return root.TrimEnd('\\').TrimEnd(':').ToUpperInvariant();
    }

    private static string DefaultFileName() =>
        $"FileStats_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

    public static string HelpText { get; } =
        "CLIFileStatistics — collects file statistics to CSV (UTF-8 with BOM).\n" +
        "\n" +
        "Usage:\n" +
        "  CLIFileStatistics [options]\n" +
        "\n" +
        "Options:\n" +
        "  -d, --drives C,D,E   Drive letters to scan, comma-separated.\n" +
        "                       If omitted, the drive containing the exe is used.\n" +
        "  -p, --path <path>    Scan specific directory(ies) instead of a whole drive.\n" +
        "                       Repeatable flag; several paths can be separated by ';'.\n" +
        "                       Mutually exclusive with -d/--drives.\n" +
        "  -s, --separator <S>  CSV field separator: ',' (default), ';', or\n" +
        "                       the words comma/semicolon.\n" +
        "  -o, --output <path>  Where to save the CSV. If a folder is given, a file\n" +
        "                       FileStats_<date>.csv is created inside. Default: next to the exe.\n" +
        "  -t, --threads <N>    Number of metadata collection threads (default: CPU count).\n" +
        "  -h, --help           Show this help.\n" +
        "  --version            Program version.\n" +
        "\n" +
        "Examples:\n" +
        "  CLIFileStatistics\n" +
        "  CLIFileStatistics -d C,D -o D:\\stats\n" +
        "  CLIFileStatistics -p \"D:\\repos\" -p \"C:\\Users\\pops\"\n" +
        "  CLIFileStatistics --path=\"D:\\repos;D:\\temp\" -s semicolon\n" +
        "  CLIFileStatistics --path=D:\\repos --output=D:\\stats\\files.csv --threads 4\n" +
        "\n" +
        "CSV columns:\n" +
        "  Full path; Type (File/Directory); Name; Extension; Disk; Directory; Created;\n" +
        "  Modified; Size bytes; Description; Application (association); Owner; Attributes;\n" +
        "  Needs admin rights (true/false); Note\n" +
        "\n" +
        "Files and directories without access (administrator rights required) are written\n" +
        "to the CSV with 'Needs admin rights' = true and the reason in 'Note'.\n";
}
