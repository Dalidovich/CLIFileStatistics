using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Win32;

namespace CLIFileStatistics.Metadata;

public sealed class FileAssociationResolver
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public string GetAssociatedApp(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "";

        var key = "." + extension.TrimStart('.').ToLowerInvariant();
        return _cache.GetOrAdd(key, Lookup);
    }

    private static string Lookup(string ext)
    {
        try
        {
            var progId = GetDefault(Registry.ClassesRoot, ext)
                         ?? GetDefault(Registry.ClassesRoot, "SystemFileAssociations" + ext);

            if (string.IsNullOrWhiteSpace(progId))
                return "";

            var command = GetDefault(Registry.ClassesRoot, progId + @"\shell\open\command");
            var exePath = ExtractExePath(command);

            if (exePath is not null)
            {
                var friendlyName = GetFriendlyName(exePath);
                if (!string.IsNullOrWhiteSpace(friendlyName))
                    return friendlyName;
                return Path.GetFileNameWithoutExtension(exePath);
            }

            return progId;
        }
        catch
        {
            return "";
        }
    }

    private static string? GetDefault(RegistryKey root, string subKey)
    {
        using var key = root.OpenSubKey(subKey);
        return key?.GetValue(null) as string;
    }

    private static string? ExtractExePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end < 0 ? null : command.Substring(1, end - 1);
        }

        var space = command.IndexOf(' ');
        return space < 0 ? command : command.Substring(0, space);
    }

    private static string GetFriendlyName(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                return info.FileDescription;
            if (!string.IsNullOrWhiteSpace(info.ProductName))
                return info.ProductName;
        }
        catch
        {
        }

        return "";
    }
}
