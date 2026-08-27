using CLIFileStatistics.Csv;
using CLIFileStatistics.Metadata;
using CLIFileStatistics.Models;

namespace CLIFileStatistics.Scanning;

public sealed class FileScanner
{
    private const int ParallelFileThreshold = 32;

    private readonly int _threads;
    private readonly string? _outputPath;
    private readonly MetadataCollector _collector = new();
    private readonly System.Diagnostics.Stopwatch _reportTimer = System.Diagnostics.Stopwatch.StartNew();
    private long _lastReportedCount;

    public FileScanner(int threads, string? outputPath)
    {
        _threads = Math.Max(1, threads);
        _outputPath = outputPath;
    }

    public ScanStats Scan(IReadOnlyList<ScanRoot> roots, CsvExporter exporter, CancellationToken token, Action<ScanStats>? report)
    {
        var stats = new ScanStats();

        try
        {
            foreach (var root in roots)
                Walk(root, exporter, stats, token, report);
        }
        catch (OperationCanceledException)
        {
        }

        return stats;
    }

    private void Walk(ScanRoot root, CsvExporter exporter, ScanStats stats, CancellationToken token, Action<ScanStats>? report)
    {
        var pending = new Stack<string>();
        pending.Push(root.Path);

        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();

            var dir = pending.Pop();

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

            WriteRecord(new ScanEntry(dir, true, accessError, null, root.Disk), exporter, stats);

            if (children is null)
                continue;

            var files = new List<ScanEntry>();
            var subdirectories = new List<string>();
            CollectChildren(children, root.Disk, files, subdirectories, exporter, stats, token);

            foreach (var record in CollectMetadata(files, token))
                WriteRecord(record, exporter, stats);

            ReportProgress(stats, report);

            subdirectories.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = subdirectories.Count - 1; i >= 0; i--)
                pending.Push(subdirectories[i]);
        }
    }

    private void CollectChildren(
        IEnumerable<string> children,
        string disk,
        List<ScanEntry> files,
        List<string> subdirectories,
        CsvExporter exporter,
        ScanStats stats,
        CancellationToken token)
    {
        foreach (var child in children)
        {
            token.ThrowIfCancellationRequested();

            if (_outputPath is not null && string.Equals(child, _outputPath, StringComparison.OrdinalIgnoreCase))
                continue;

            FileAttributes attr;
            try
            {
                attr = File.GetAttributes(child);
            }
            catch
            {
                var entry = new ScanEntry(child, Directory.Exists(child), "Access denied while reading attributes", null, disk);
                if (entry.IsDirectory)
                    WriteRecord(entry, exporter, stats);
                else
                    files.Add(entry);
                continue;
            }

            if ((attr & FileAttributes.Directory) == 0)
            {
                files.Add(new ScanEntry(child, false, null, null, disk));
                continue;
            }

            if ((attr & FileAttributes.ReparsePoint) != 0)
            {
                WriteRecord(new ScanEntry(child, true, null, "Reparse point (link), recursive walk skipped", disk), exporter, stats);
                continue;
            }

            subdirectories.Add(child);
        }
    }

    private List<FileStatRecord> CollectMetadata(List<ScanEntry> files, CancellationToken token)
    {
        var records = files.Count >= ParallelFileThreshold && _threads > 1
            ? files
                .AsParallel()
                .WithDegreeOfParallelism(_threads)
                .WithCancellation(token)
                .Select(_collector.Collect)
                .ToList()
            : files.Select(_collector.Collect).ToList();

        records.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.FullPath, right.FullPath));
        return records;
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
        UnauthorizedAccessException => "Access denied (administrator rights required)",
        PathTooLongException => "Path too long",
        _ => ex.Message
    };

    private void ReportProgress(ScanStats stats, Action<ScanStats>? report)
    {
        if (report is null)
            return;

        var count = stats.Total;
        if (count - _lastReportedCount < 5000 && _reportTimer.ElapsedMilliseconds < 1000)
            return;

        _lastReportedCount = count;
        _reportTimer.Restart();
        report(stats);
    }
}
