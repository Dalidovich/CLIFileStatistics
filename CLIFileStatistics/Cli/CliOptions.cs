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
                        return ParseResult.Fail($"Аргументу {arg} требуется значение: список букв дисков через запятую.");
                    var driveError = options.SetDrives(drives);
                    if (driveError is not null)
                        return ParseResult.Fail(driveError);
                    break;
                }

                case "-p":
                case "--path":
                {
                    if (!TryTakeValue(args, ref i, out var path))
                        return ParseResult.Fail($"Аргументу {arg} требуется значение: путь к каталогу.");
                    options.AddPath(path);
                    break;
                }

                case "-s":
                case "--separator":
                {
                    if (!TryTakeValue(args, ref i, out var separator))
                        return ParseResult.Fail($"Аргументу {arg} требуется значение: ',' ';' или comma/semicolon.");
                    var separatorError = options.SetSeparator(separator);
                    if (separatorError is not null)
                        return ParseResult.Fail(separatorError);
                    break;
                }

                case "-o":
                case "--output":
                {
                    if (!TryTakeValue(args, ref i, out var output))
                        return ParseResult.Fail($"Аргументу {arg} требуется значение: путь к CSV-файлу.");
                    options.OutputPath = output;
                    break;
                }

                case "-t":
                case "--threads":
                {
                    if (!TryTakeValue(args, ref i, out var threads))
                        return ParseResult.Fail($"Аргументу {arg} требуется значение: число потоков.");
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

                    return ParseResult.Fail($"Неизвестный аргумент: {arg}");
            }
        }

        if (options.DrivesSpecified && options.PathsSpecified)
            return ParseResult.Fail("Флаги -d/--drives и -p/--path взаимоисключающие: укажите что-то одно.");

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
                    Console.Error.WriteLine($"Диск {letter}: не найден — пропущен.");
                    continue;
                }
                if (!drive.IsReady)
                {
                    Console.Error.WriteLine($"Диск {letter}: не готов (нет носителя) — пропущен.");
                    continue;
                }
                result.Add(new ScanRoot(drive.Name, drive.Name.TrimEnd('\\')));
            }
            catch
            {
                Console.Error.WriteLine($"Диск {letter}: не найден — пропущен.");
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
                    Console.Error.WriteLine($"Каталог {scanPath}: не найден — пропущен.");
                    continue;
                }
                var disk = Path.GetPathRoot(fullPath)?.TrimEnd('\\') ?? "";
                result.Add(new ScanRoot(fullPath, disk));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Каталог {scanPath}: неверный путь — {ex.Message}");
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
            return $"Некорректное число потоков: '{value}'. Ожидается целое от 1 до 512.";
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

        return $"Некорректный разделитель: '{value}'. Укажите ',' ';' или слово comma/semicolon.";
    }

    private string? SetDrives(string value)
    {
        DrivesSpecified = true;

        var parts = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);        foreach (var part in parts)
        {
            var letter = part.TrimEnd(':').Trim().ToUpperInvariant();
            if (letter.Length != 1 || !char.IsLetter(letter[0]))
                return $"Некорректное обозначение диска: '{part}'. Указывайте только буквы, например C,D,E.";
            if (!_driveLetters.Contains(letter))
                _driveLetters.Add(letter);
        }

        if (_driveLetters.Count == 0)
            return "Не задано ни одной буквы диска.";

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
        "CLIFileStatistics — сбор статистики по файлам диска в CSV (UTF-8 с BOM).\n" +
        "\n" +
        "Использование:\n" +
        "  CLIFileStatistics [опции]\n" +
        "\n" +
        "Опции:\n" +
        "  -d, --drives C,D,E   Буквы дисков для сканирования через запятую.\n" +
        "                       Если не указано — берётся диск, на котором находится exe.\n" +
        "  -p, --path <путь>    Сканировать конкретный каталог(и), а не весь диск.\n" +
        "                       Флаг повторяемый; несколько путей можно через ';'.\n" +
        "                       Несовместим с -d/--drives.\n" +
        "  -s, --separator <S>  Разделитель полей CSV: ',' (по умолчанию), ';', или\n" +
        "                       слово comma/semicolon. Для Excel RU удобнее ';'.\n" +
        "  -o, --output <путь>  Куда сохранить CSV. Если указана папка — внутри создастся\n" +
        "                       файл FileStats_<дата>.csv. По умолчанию — рядом с exe.\n" +
        "  -t, --threads <N>    Число потоков сбора метаданных (по умолчанию — число ядер).\n" +
        "  -h, --help           Показать эту справку.\n" +
        "  --version            Версия программы.\n" +
        "\n" +
        "Примеры:\n" +
        "  CLIFileStatistics\n" +
        "  CLIFileStatistics -d C,D -o D:\\stats\n" +
        "  CLIFileStatistics -p \"D:\\repos\" -p \"C:\\Users\\pops\"\n" +
        "  CLIFileStatistics --path=\"D:\\repos;D:\\temp\" -s semicolon\n" +
        "  CLIFileStatistics --path=D:\\repos --output=D:\\stats\\files.csv --threads 4\n" +
        "\n" +
        "Колонки CSV:\n" +
        "  Полный путь; Тип (File/Directory); Имя; Расширение; Диск; Директория; Создан;\n" +
        "  Изменен; Размер байт; Описание; Приложение (ассоциация); Владелец; Атрибуты;\n" +
        "  Нужны права админа (true/false); Примечание\n" +
        "\n" +
        "Файлы и каталоги, к которым нет доступа (нужны права администратора), записываются\n" +
        "в CSV с пометкой 'Нужны права админа' = true и причиной в 'Примечание'.\n";
}
