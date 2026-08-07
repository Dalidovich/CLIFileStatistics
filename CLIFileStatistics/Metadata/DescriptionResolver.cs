using System.Diagnostics;

namespace CLIFileStatistics.Metadata;

public sealed class DescriptionResolver
{
    public string GetDescription(string filePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(filePath);
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
