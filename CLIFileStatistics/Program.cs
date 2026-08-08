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
            Console.Error.WriteLine("Error: " + parsed.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 2;
        }

        var options = parsed.Options!;

        var roots = options.PathsSpecified ? options.ResolvePaths() : options.ResolveDrives();
        if (roots.Count == 0)
        {
            Console.Error.WriteLine("No accessible disks or directories found to scan.");
            return 2;
        }

        string outputPath;
        try
        {
            outputPath = options.ResolveOutputPath();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error building output path: " + ex.Message);
            return 2;
        }

        Console.WriteLine("CLIFileStatistics — file statistics to CSV");
        Console.WriteLine($"Disks/Paths: {string.Join(", ", roots.Select(r => r.Path))}");
        Console.WriteLine($"Output file: {outputPath}");
        Console.WriteLine($"Separator:   {options.Separator}");
        Console.WriteLine($"Threads:     {options.Threads}");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine();
            Console.WriteLine("Interrupt received. Saving already collected data...");
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
                Console.WriteLine("Done.");
                Console.WriteLine($"Rows processed:    {stats.Total:N0}  (files: {stats.Files:N0}, directories: {stats.Directories:N0})");
                Console.WriteLine($"Needs admin rights: {stats.NeedsAdmin:N0}");
                Console.WriteLine($"Elapsed:           {stopwatch.Elapsed:hh\\:mm\\:ss}");
                Console.WriteLine($"File saved:        {outputPath}");

                return cts.IsCancellationRequested ? 1 : 0;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("No access to the output file: " + ex.Message);
                return 1;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Output write error: " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Unexpected error: " + ex.Message);
                return 1;
            }
        }
    }

    private static void ReportProgress(ScanStats stats)
    {
        var text =
            $"Processed: {stats.Total:N0}  |  files: {stats.Files:N0}  |  " +
            $"directories: {stats.Directories:N0}  |  no access: {stats.NeedsAdmin:N0}";

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
