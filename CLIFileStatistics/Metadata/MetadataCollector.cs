using CLIFileStatistics.Models;
using CLIFileStatistics.Scanning;

namespace CLIFileStatistics.Metadata;

public sealed class MetadataCollector
{
    private readonly OwnerHelper _ownerHelper = new();
    private readonly DescriptionResolver _descriptionResolver = new();
    private readonly FileAssociationResolver _associationResolver = new();

    public FileStatRecord Collect(ScanEntry entry)
    {
        var path = entry.FullPath;
        var isDirectory = entry.IsDirectory;

        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            name = path;

        var extension = "";
        if (!isDirectory)
            extension = Path.GetExtension(name).TrimStart('.');

        var directoryPath = Path.GetDirectoryName(path);
        var drive = entry.Disk;

        DateTime? created = null;
        DateTime? modified = null;
        long? sizeBytes = null;
        var attributes = "";

        try
        {
            if (isDirectory)
            {
                var info = new DirectoryInfo(path);
                created = info.CreationTime;
                modified = info.LastWriteTime;
                attributes = info.Attributes.ToString();
            }
            else
            {
                var info = new FileInfo(path);
                created = info.CreationTime;
                modified = info.LastWriteTime;
                sizeBytes = info.Length;
                attributes = info.Attributes.ToString();
            }
        }
        catch
        {
        }

        var description = isDirectory ? "" : _descriptionResolver.GetDescription(path);
        var associatedApp = !isDirectory && extension.Length > 0
            ? _associationResolver.GetAssociatedApp(extension)
            : "";

        var needsAdmin = !string.IsNullOrEmpty(entry.AccessError);
        var notes = new List<string>();
        if (!string.IsNullOrEmpty(entry.AccessError))
            notes.Add(entry.AccessError);
        if (!string.IsNullOrEmpty(entry.InfoNote))
            notes.Add(entry.InfoNote);

        var (owner, ownerDenied) = _ownerHelper.GetOwner(path, isDirectory);
        if (ownerDenied)
        {
            needsAdmin = true;
            notes.Add("Access denied while reading the owner");
        }

        return new FileStatRecord
        {
            FullPath = path,
            IsDirectory = isDirectory,
            Name = name,
            Extension = extension,
            Drive = drive,
            DirectoryPath = directoryPath ?? "",
            Created = created,
            Modified = modified,
            SizeBytes = sizeBytes,
            Description = description,
            AssociatedApp = associatedApp,
            Owner = owner,
            Attributes = attributes,
            NeedsAdmin = needsAdmin,
            Note = string.Join("; ", notes)
        };
    }
}
