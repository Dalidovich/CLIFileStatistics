using System.Text;
using CLIFileStatistics.Cli;
using CLIFileStatistics.Csv;
using CLIFileStatistics.Models;
using CLIFileStatistics.Scanning;

namespace CLIFileStatistics;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
        }
        catch
        {
        }

        var parsed = CliOptions.Parse(args);

        if (parsed.HelpRequested)
        {
            Console.WriteLine(CliOptions.HelpText);
            return 0;
        }

        if (parsed.VersionRequested)
        {
            Console.WriteLine($"CLIFileStatistics {typeof(Program).Assembly.GetName().Version}");
            return 0;
        }

        if (!parsed.Success)
        {
            Console.Error.WriteLine("Ошибка: " + parsed.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 2;
        }

        var options = parsed.Options!;

        var roots = options.PathsSpecified ? options.ResolvePaths() : options.ResolveDrives();
        if (roots.Count == 0)
        {
            Console.Error.WriteLine("Не найдено ни одного доступного диска или каталога для сканирования.");
            return 2;
        }

        string outputPath;
        try
        {
            outputPath = options.ResolveOutputPath();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ошибка формирования пути вывода: " + ex.Message);
            return 2;
        }

        Console.WriteLine("CLIFileStatistics — статистика файлов в CSV");
        Console.WriteLine($"Диски/пути:  {string.Join(", ", roots.Select(r => r.Path))}");
        Console.WriteLine($"Файл вывода: {outputPath}");
        Console.WriteLine($"Разделитель: {options.Separator}");
        Console.WriteLine($"Потоков:     {options.Threads}");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine();
            Console.WriteLine("Получена команда прерывания. Сохраняю уже собранные данные...");
        };

        var scanner = new FileScanner(options.Threads);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using (var exporter = new CsvExporter(outputPath, options.Separator))
        {
            try
            {
                var stats = scanner.Scan(roots, exporter, cts.Token, ReportProgress);
                stopwatch.Stop();

                Console.WriteLine();
                Console.WriteLine("Готово.");
                Console.WriteLine($"Обработано строк:   {stats.Total:N0}  (файлов: {stats.Files:N0}, каталогов: {stats.Directories:N0})");
                Console.WriteLine($"Нужны права админа: {stats.NeedsAdmin:N0}");
                Console.WriteLine($"Затрачено времени:  {stopwatch.Elapsed:hh\\:mm\\:ss}");
                Console.WriteLine($"Файл сохранён:      {outputPath}");

                return cts.IsCancellationRequested ? 1 : 0;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Нет доступа к файлу вывода: " + ex.Message);
                return 1;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Ошибка записи вывода: " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Непредвиденная ошибка: " + ex.Message);
                return 1;
            }
        }
    }

    private static void ReportProgress(ScanStats stats)
    {
        var text =
            $"Обработано: {stats.Total:N0}  |  файлов: {stats.Files:N0}  |  " +
            $"каталогов: {stats.Directories:N0}  |  без доступа: {stats.NeedsAdmin:N0}";

        int width;
        try
        {
            width = Console.BufferWidth;
        }
        catch
        {
            width = 0;
        }

        if (width > 0)
            text = text.PadRight(width - 1);

        Console.Write("\r" + text);
    }
}
