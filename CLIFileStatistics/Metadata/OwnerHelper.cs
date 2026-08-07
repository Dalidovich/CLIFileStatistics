using System.Collections.Concurrent;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CLIFileStatistics.Metadata;

public sealed class OwnerHelper
{
    private readonly ConcurrentDictionary<string, string> _nameCache = new();

    public (string Owner, bool AccessDenied) GetOwner(string path, bool isDirectory)
    {
        try
        {
            IdentityReference? owner = isDirectory
                ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier))
                : new FileInfo(path).GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));

            if (owner is SecurityIdentifier sid)
                return (ResolveName(sid), false);
            return (owner?.Value ?? "", false);
        }
        catch (UnauthorizedAccessException)
        {
            return ("", true);
        }
        catch (SecurityException)
        {
            return ("", true);
        }
        catch
        {
            return ("", false);
        }
    }

    private string ResolveName(SecurityIdentifier sid)
    {
        var key = sid.Value;
        if (_nameCache.TryGetValue(key, out var cached))
            return cached;

        var name = key;
        try
        {
            name = sid.Translate(typeof(NTAccount)).Value;
        }
        catch
        {
        }

        _nameCache.TryAdd(key, name);
        return name;
    }
}
