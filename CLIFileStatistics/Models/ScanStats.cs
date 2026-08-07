namespace CLIFileStatistics.Models;

public sealed class ScanStats
{
    private long _total;
    private long _files;
    private long _directories;
    private long _needsAdmin;

    public long Total => Interlocked.Read(ref _total);
    public long Files => Interlocked.Read(ref _files);
    public long Directories => Interlocked.Read(ref _directories);
    public long NeedsAdmin => Interlocked.Read(ref _needsAdmin);

    public void Add(FileStatRecord record)
    {
        Interlocked.Increment(ref _total);
        if (record.IsDirectory)
            Interlocked.Increment(ref _directories);
        else
            Interlocked.Increment(ref _files);
        if (record.NeedsAdmin)
            Interlocked.Increment(ref _needsAdmin);
    }
}
