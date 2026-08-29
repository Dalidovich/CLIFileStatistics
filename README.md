# CLIFileStatistics

CLI-утилита для .NET 9 (Windows), собирающая статистику по файлам и папкам на диске(ах) или в указанных каталогах и сохраняющая результат в CSV (UTF-8 с BOM).

## Возможности

- Сканирование целых дисков (по умолчанию — диска, на котором лежит exe) либо конкретных каталогов.
- Многопоточный сбор метаданных (количество потоков настраивается).
- Для каждого файла/папки собираются: полный путь, тип, имя, расширение, диск, каталог, даты создания/изменения, размер, описание, приложение-ассоциация, владелец, атрибуты.
- Объекты без доступа (нужны права администратора) записываются в CSV с пометкой `Needs admin rights = true` и причиной в `Note`, а не приводят к падению сканирования.
- Корректная остановка по Ctrl+C с сохранением уже собранных данных.
- Настраиваемый разделитель CSV-полей.

## Требования

- .NET 9 SDK (таргет `net9.0-windows`).
- Windows.

## Сборка

```powershell
dotnet build CLIFileStatistics.sln -c Release
```

## Запуск

```powershell
CLIFileStatistics [options]
```

### Опции

| Флаг | Описание |
|---|---|
| `-d, --drives C,D,E` | Буквы дисков для сканирования через запятую. Если не указано — используется диск, на котором находится exe. |
| `-p, --path <path>` | Сканировать конкретный(е) каталог(и) вместо целого диска. Флаг можно повторять; несколько путей также можно перечислить через `;`. Несовместим с `-d/--drives`. |
| `-s, --separator <S>` | Разделитель полей CSV: `,` (по умолчанию), `;`, `\|`, либо слова `comma`/`semicolon`/`tab`/`pipe`. |
| `-o, --output <path>` | Путь для сохранения CSV. Если указана папка — внутри создаётся файл `FileStats_<дата>.csv`. Расширение `.csv` добавляется принудительно. По умолчанию — рядом с exe. |
| `-t, --threads <N>` | Количество потоков сбора метаданных (по умолчанию — число ядер CPU). |
| `-h, --help` | Показать справку. |
| `--version` | Показать версию программы. |

### Примеры

```powershell
CLIFileStatistics
CLIFileStatistics -d C,D -o D:\stats
CLIFileStatistics -p "D:\repos" -p "C:\Users\pops"
CLIFileStatistics --path="D:\repos;D:\temp" -s semicolon
CLIFileStatistics --path=D:\repos --output=D:\stats\files.csv --threads 4
```

## Формат CSV

Столбцы: `Full path`, `Type` (File/Directory), `Name`, `Extension`, `Disk`, `Directory`, `Created`, `Modified`, `Size bytes`, `Description`, `Application`, `Owner`, `Attributes`, `Needs admin rights` (true/false), `Note`.

## Структура проекта

- `Program.cs` — точка входа, оркестрация сканирования и вывод прогресса.
- `Cli/` — разбор аргументов командной строки (`CliOptions`, `ParseResult`).
- `Scanning/` — обход файловой системы (`FileScanner`, `ScanEntry`, `ScanRoot`).
- `Metadata/` — сбор метаданных файлов: описание, ассоциированное приложение, владелец (`DescriptionResolver`, `FileAssociationResolver`, `MetadataCollector`, `OwnerHelper`).
- `Csv/` — запись результатов в CSV (`CsvExporter`).
- `Models/` — модели данных (`FileStatRecord`, `ScanStats`).
