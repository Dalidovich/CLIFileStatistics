namespace CLIFileStatistics.Scanning;

public sealed class ScanRoot
{
    public ScanRoot(string path, string disk)
    {
        Path = path;
        Disk = disk;
    }

    public string Path { get; }
    public string Disk { get; }
}
