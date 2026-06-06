using System.Text.Json;
using Microsoft.Win32;
using DeleteApp.Core.Quarantine;
using DeleteApp.Data.Models;
using DeleteApp.Utils.Hash;
using DeleteApp.Utils.Logger;
using DeleteApp.Utils.PathSafe;

namespace DeleteApp.Core.Cleaner;

public sealed class StartupCleaner
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly ILocalLogger _logger;
    private readonly QuarantineStore _store;

    public StartupCleaner(ILocalLogger logger, QuarantineStore store)
    {
        _logger = logger;
        _store = store;
    }

    public async Task<IReadOnlyList<OperationRecord>> DisableStartupAsync(IReadOnlyList<ScanItem> items, CancellationToken cancellationToken)
    {
        var records = new List<OperationRecord>();
        var quarantine = (await _store.LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.Source is not (ScanSource.StartupRegistry or ScanSource.StartupFolder))
            {
                records.Add(new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "DisableStartup", false, "仅支持启动项（注册表 Run / Startup 文件夹）"));
                continue;
            }

            try
            {
                if (item.Source == ScanSource.StartupRegistry)
                {
                    DisableRegistryRun(item, quarantine);
                    records.Add(new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "DisableStartup", true, null));
                }
                else
                {
                    DisableStartupFolderFile(item, quarantine);
                    records.Add(new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "DisableStartup", true, null));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Disable startup failed: {item.Source} {item.Name}", ex);
                records.Add(new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "DisableStartup", false, ex.Message));
            }
        }

        await _store.SaveAsync(quarantine, cancellationToken).ConfigureAwait(false);
        return records;
    }

    public async Task<IReadOnlyList<OperationRecord>> RestoreAsync(IReadOnlyList<QuarantineEntry> entries, CancellationToken cancellationToken)
    {
        var records = new List<OperationRecord>();
        var quarantine = (await _store.LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var index = quarantine.ToDictionary(e => e.Id, e => e);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!index.TryGetValue(entry.Id, out var stored))
            {
                records.Add(new OperationRecord(DateTimeOffset.Now, entry.Id, ScanSource.StartupFolder, entry.Name, "Restore", false, "隔离区记录不存在或已被移除"));
                continue;
            }

            try
            {
                if (stored.EntryType == QuarantineEntryType.StartupFolderFile)
                {
                    RestoreStartupFolderFile(stored);
                }
                else
                {
                    RestoreRegistryRun(stored);
                }

                quarantine.RemoveAll(e => e.Id == stored.Id);
                TryDeleteBackupFile(stored.QuarantineLocation);
                records.Add(new OperationRecord(DateTimeOffset.Now, entry.Id, GuessScanSource(stored.EntryType), entry.Name, "Restore", true, null));
            }
            catch (Exception ex)
            {
                _logger.Error($"Restore failed: {stored.EntryType} {stored.Name}", ex);
                records.Add(new OperationRecord(DateTimeOffset.Now, entry.Id, GuessScanSource(stored.EntryType), entry.Name, "Restore", false, ex.Message));
            }
        }

        await _store.SaveAsync(quarantine, cancellationToken).ConfigureAwait(false);
        return records;
    }

    private void DisableRegistryRun(ScanItem item, List<QuarantineEntry> quarantine)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("无法打开 HKCU Run 注册表键");
        }

        var valueName = item.Name;
        var existing = key.GetValue(valueName) as string;
        if (string.IsNullOrWhiteSpace(existing))
        {
            throw new InvalidOperationException("启动项不存在或值为空");
        }

        var id = Guid.NewGuid();
        var backupPath = Path.Combine(_store.RootDirectory, $"registry_run_{id:N}.json");
        File.WriteAllText(backupPath, JsonSerializer.Serialize(new RegistryRunBackup(RunKeyPath, valueName, existing), new JsonSerializerOptions { WriteIndented = true }));

        key.DeleteValue(valueName, throwOnMissingValue: false);

        quarantine.Add(new QuarantineEntry(
            id,
            QuarantineEntryType.StartupRegistryRunValue,
            DateTimeOffset.Now,
            item.RiskLevel,
            item.Name,
            item.Publisher,
            $@"HKCU\{RunKeyPath}\{valueName}",
            backupPath,
            existing,
            null,
            null,
            "将备份的 Run 值写回注册表并移除备份"));

        _logger.Info($"Disabled startup registry value: {valueName}");
    }

    private void DisableStartupFolderFile(ScanItem item, List<QuarantineEntry> quarantine)
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var originalPath = Path.Combine(startupFolder, item.Name);

        if (!PathSafe.IsUnderDirectory(originalPath, startupFolder))
        {
            throw new InvalidOperationException("启动项文件路径校验失败");
        }

        if (!PathSafe.IsFilePathSafe(originalPath))
        {
            throw new FileNotFoundException("启动项文件不存在", originalPath);
        }

        var id = Guid.NewGuid();
        var quarantinePath = _store.CreateQuarantineFilePath(originalPath);
        var fileInfo = new FileInfo(originalPath);
        var sha256 = Sha256Hasher.HashFile(originalPath);

        File.Move(originalPath, quarantinePath);

        quarantine.Add(new QuarantineEntry(
            id,
            QuarantineEntryType.StartupFolderFile,
            DateTimeOffset.Now,
            item.RiskLevel,
            item.Name,
            item.Publisher,
            originalPath,
            quarantinePath,
            item.CommandLine,
            sha256,
            fileInfo.Length,
            "将文件从隔离区移动回 Startup 文件夹原路径"));

        _logger.Info($"Disabled startup folder item: {originalPath}");
    }

    private void RestoreStartupFolderFile(QuarantineEntry entry)
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (!PathSafe.IsUnderDirectory(entry.OriginalLocation, startupFolder))
        {
            throw new InvalidOperationException("还原目标不在 Startup 文件夹内，已阻止");
        }

        if (!File.Exists(entry.QuarantineLocation))
        {
            throw new FileNotFoundException("隔离区文件不存在", entry.QuarantineLocation);
        }

        if (File.Exists(entry.OriginalLocation))
        {
            throw new IOException("原路径已存在同名文件，无法覆盖");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalLocation)!);
        File.Move(entry.QuarantineLocation, entry.OriginalLocation);
        _logger.Info($"Restored startup folder item: {entry.OriginalLocation}");
    }

    private void RestoreRegistryRun(QuarantineEntry entry)
    {
        if (!File.Exists(entry.QuarantineLocation))
        {
            throw new FileNotFoundException("注册表备份文件不存在", entry.QuarantineLocation);
        }

        var json = File.ReadAllText(entry.QuarantineLocation);
        var backup = JsonSerializer.Deserialize<RegistryRunBackup>(json);
        if (backup is null || string.IsNullOrWhiteSpace(backup.ValueName) || string.IsNullOrWhiteSpace(backup.ValueData))
        {
            throw new InvalidOperationException("注册表备份文件内容无效");
        }

        if (!string.Equals(backup.KeyPath, RunKeyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("备份键路径不在允许范围内，已阻止");
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("无法打开 HKCU Run 注册表键");
        }

        key.SetValue(backup.ValueName, backup.ValueData, RegistryValueKind.String);
        _logger.Info($"Restored startup registry value: {backup.ValueName}");
    }

    private static void TryDeleteBackupFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static ScanSource GuessScanSource(QuarantineEntryType type) => type switch
    {
        QuarantineEntryType.StartupRegistryRunValue => ScanSource.StartupRegistry,
        QuarantineEntryType.StartupFolderFile => ScanSource.StartupFolder,
        _ => ScanSource.StartupFolder
    };

    private sealed record RegistryRunBackup(string KeyPath, string ValueName, string ValueData);
}
