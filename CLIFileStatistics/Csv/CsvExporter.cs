using System.Text;
using CLIFileStatistics.Models;

namespace CLIFileStatistics.Csv;

public sealed class CsvExporter : IDisposable
{
    private const int FlushEveryRows = 10000;

    private readonly StreamWriter _writer;
    private readonly object _sync = new();
    private readonly char _separator;
    private int _rowsWritten;

    public CsvExporter(string filePath, char separator)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _separator = separator;
        var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, new UTF8Encoding(true));
        WriteLine(string.Join(_separator.ToString(), FileStatRecord.Header));
    }

    public void WriteRow(FileStatRecord record)
    {
        var line = string.Join(_separator.ToString(), record.ToCsvFields().Select(f => Escape(f, _separator)));
        lock (_sync)
        {
            WriteLine(line);
            if (++_rowsWritten % FlushEveryRows == 0)
                _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }

    private void WriteLine(string line)
    {
        _writer.Write(line);
        _writer.Write(Environment.NewLine);
    }

    private static string Escape(string? field, char separator)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        if (field.IndexOf(separator) >= 0 || field.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
            return "\"" + field.Replace("\"", "\"\"") + "\"";

        return field;
    }
}
