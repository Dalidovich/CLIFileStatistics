using System.Globalization;

namespace CLIFileStatistics.Models;

public sealed class FileStatRecord
{
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }
    public string Name { get; init; } = "";
    public string Extension { get; init; } = "";
    public string Drive { get; init; } = "";
    public string DirectoryPath { get; init; } = "";
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public long? SizeBytes { get; init; }
    public string Description { get; init; } = "";
    public string AssociatedApp { get; init; } = "";
    public string Owner { get; init; } = "";
    public string Attributes { get; init; } = "";
    public bool NeedsAdmin { get; init; }
    public string Note { get; init; } = "";

    public static readonly string[] Header =
    {
        "Полный путь",
        "Тип",
        "Имя",
        "Расширение",
        "Диск",
        "Директория",
        "Создан",
        "Изменен",
        "Размер байт",
        "Описание",
        "Приложение",
        "Владелец",
        "Атрибуты",
        "Нужны права админа",
        "Примечание"
    };

    public string?[] ToCsvFields() => new string?[]
    {
        FullPath,
        IsDirectory ? "Directory" : "File",
        Name,
        Extension,
        Drive,
        DirectoryPath,
        FormatDate(Created),
        FormatDate(Modified),
        SizeBytes?.ToString(CultureInfo.InvariantCulture),
        Description,
        AssociatedApp,
        Owner,
        Attributes,
        NeedsAdmin ? "true" : "false",
        Note
    };

    private static string? FormatDate(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
