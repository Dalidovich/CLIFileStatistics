namespace CLIFileStatistics.Scanning;

public sealed class ScanEntry
{
    public ScanEntry(string fullPath, bool isDirectory, string? accessError, string? infoNote, string disk)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        AccessError = accessError;
        InfoNote = infoNote;
        Disk = disk;
    }

    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string? AccessError { get; }
    public string? InfoNote { get; }
    public string Disk { get; }
}
