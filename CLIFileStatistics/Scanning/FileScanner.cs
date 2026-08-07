using CLIFileStatistics.Csv;
using CLIFileStatistics.Metadata;
using CLIFileStatistics.Models;

namespace CLIFileStatistics.Scanning;

public sealed class FileScanner
{
    private readonly int _threads;
    private readonly MetadataCollector _collector = new();
    private readonly object _reportSync = new();
    private readonly System.Diagnostics.Stopwatch _reportTimer = System.Diagnostics.Stopwatch.StartNew();
    private long _lastReportedCount;

    public FileScanner(int threads)
    {
        _threads = Math.Max(1, threads);
    }

    public ScanStats Scan(IReadOnlyList<ScanRoot> roots, CsvExporter exporter, CancellationToken token, Action<ScanStats>? report)
    {
        var stats = new ScanStats();

        try
        {
            foreach (var root in roots)
                Walk(root.Path, root.Disk, exporter, stats, token, report);
        }
        catch (OperationCanceledException)
        {
        }

        return stats;
    }

    private void Walk(string dir, string disk, CsvExporter exporter, ScanStats stats, CancellationToken token, Action<ScanStats>? report)
    {
        token.ThrowIfCancellationRequested();

        string? accessError = null;
        IEnumerable<string>? children = null;
        try
        {
            children = Directory.EnumerateFileSystemEntries(dir);
        }
        catch (Exception ex)
        {
            accessError = Classify(ex);
        }

        WriteRecord(new ScanEntry(dir, true, accessError, null, disk), exporter, stats);

        if (children is null)
            return;

        var files = new List<ScanEntry>();
        var subdirectories = new List<string>();

        foreach (var child in children)
        {
            token.ThrowIfCancellationRequested();

            FileAttributes attr;
            try
            {
                attr = File.GetAttributes(child);
            }
            catch
            {
                files.Add(new ScanEntry(child, false, "Отказ в доступе при чтении атрибутов", null, disk));
                continue;
            }

            var isDirectory = (attr & FileAttributes.Directory) != 0;

            if (isDirectory)
            {
                if ((attr & FileAttributes.ReparsePoint) != 0)
                {
                    WriteRecord(new ScanEntry(child, true, null, "Репарс-точка (ссылка), рекурсивный обход пропущен", disk), exporter, stats);
                    continue;
                }

                subdirectories.Add(child);
            }
            else
            {
                files.Add(new ScanEntry(child, false, null, null, disk));
            }
        }

        subdirectories.Sort(StringComparer.OrdinalIgnoreCase);

        var fileRecords = files
            .AsParallel()
            .WithDegreeOfParallelism(_threads)
            .WithCancellation(token)
            .Select(_collector.Collect)
            .OrderBy(r => r.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var record in fileRecords)
            WriteRecord(record, exporter, stats);

        ReportProgress(stats, report);

        foreach (var subdirectory in subdirectories)
            Walk(subdirectory, disk, exporter, stats, token, report);
    }

    private void WriteRecord(FileStatRecord record, CsvExporter exporter, ScanStats stats)
    {
        exporter.WriteRow(record);
        stats.Add(record);
    }

    private void WriteRecord(ScanEntry entry, CsvExporter exporter, ScanStats stats)
    {
        WriteRecord(_collector.Collect(entry), exporter, stats);
    }

    private static string Classify(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Отказ в доступе (требуются права администратора)",
        PathTooLongException => "Путь слишком длинный",
        _ => ex.Message
    };

    private void ReportProgress(ScanStats stats, Action<ScanStats>? report)
    {
        if (report is null)
            return;

        var count = stats.Total;
        if (count - _lastReportedCount < 5000 && _reportTimer.ElapsedMilliseconds < 1000)
            return;

        lock (_reportSync)
        {
            if (count - _lastReportedCount < 5000 && _reportTimer.ElapsedMilliseconds < 1000)
                return;
            _lastReportedCount = count;
            _reportTimer.Restart();
        }

        report(stats);
    }
}
