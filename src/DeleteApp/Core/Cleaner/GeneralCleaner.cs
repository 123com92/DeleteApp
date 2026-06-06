using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;
using DeleteApp.Core.Quarantine;
using DeleteApp.Data.Models;
using DeleteApp.Utils.Hash;
using DeleteApp.Utils.Logger;
using DeleteApp.Utils.PathSafe;

namespace DeleteApp.Core.Cleaner;

public sealed class GeneralCleaner
{
    private readonly ILocalLogger _logger;
    private readonly QuarantineStore _store;

    public GeneralCleaner(ILocalLogger logger, QuarantineStore store)
    {
        _logger = logger;
        _store = store;
    }

    public async Task<IReadOnlyList<OperationRecord>> ExecuteAsync(IReadOnlyList<ScanItem> items, CancellationToken cancellationToken)
    {
        var records = new List<OperationRecord>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                switch (item.RecommendedAction)
                {
                    case RecommendedAction.StopProcess:
                        records.Add(StopProcess(item));
                        break;
                    case RecommendedAction.DisableService:
                        records.Add(DisableService(item));
                        break;
                    case RecommendedAction.DisableTask:
                        records.Add(DisableTask(item));
                        break;
                    case RecommendedAction.DisableStartup:
                        records.Add(DisableStartup(item));
                        break;
                    case RecommendedAction.QuarantineFile:
                        records.Add(QuarantineFile(item));
                        break;
                    case RecommendedAction.Uninstall:
                        records.Add(new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "Uninstall", false, "请通过控制面板手动卸载"));
                        break;
                    default:
                        records.Add(new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, item.RecommendedAction.ToString(), false, "不支持的操作"));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Clean action failed: {item.Source} {item.Name}", ex);
                records.Add(new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, item.RecommendedAction.ToString(), false, ex.Message));
            }
        }

        return records;
    }

    private OperationRecord StopProcess(ScanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new InvalidOperationException("进程名称为空");
        }

        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(item.Name));
        var stopped = 0;
        foreach (var p in processes)
        {
            try
            {
                p.Kill();
                p.WaitForExit(5000);
                stopped++;
                _logger.Info($"Process stopped: {p.ProcessName} (PID: {p.Id})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to stop process: {p.ProcessName}", ex);
            }
        }

        if (stopped == 0)
        {
            return new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "StopProcess", false, "未找到运行中的进程");
        }

        return new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "StopProcess", true, null);
    }

    private OperationRecord DisableService(ScanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new InvalidOperationException("服务名称为空");
        }

        var serviceName = ExtractServiceName(item);
        using var sc = new ServiceController(serviceName);

        try
        {
            if (sc.CanStop && sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                _logger.Info($"Service stopped: {serviceName}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Service stop attempted: {serviceName} ({ex.Message})");
        }

        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);
        if (key is null)
        {
            throw new InvalidOperationException($"无法访问服务注册表项: {serviceName}");
        }

        var currentStart = (int)(key.GetValue("Start") ?? 2);
        if (currentStart == 4)
        {
            return new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "DisableService", true, "服务已处于禁用状态");
        }

        key.SetValue("Start", 4, RegistryValueKind.DWord);
        _logger.Info($"Service disabled: {serviceName}");

        var quarantine = _store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult().ToList();
        quarantine.Add(new QuarantineEntry(
            Guid.NewGuid(),
            QuarantineEntryType.ServiceConfigBackup,
            DateTimeOffset.Now,
            item.RiskLevel,
            item.Name,
            item.Publisher,
            $@"HKLM\SYSTEM\CurrentControlSet\Services\{serviceName}\Start",
            serviceName,
            null,
            null,
            null,
            $"将 Start 值恢复为 {currentStart}"));
        _store.SaveAsync(quarantine, CancellationToken.None).GetAwaiter().GetResult();

        return new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "DisableService", true, null);
    }

    private OperationRecord DisableTask(ScanItem item)
    {
        var taskName = item.Name.TrimStart('\\');
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new InvalidOperationException("计划任务名称为空");
        }

        var args = $"/disable /tn \"{taskName}\"";
        RunSchTasks(args);

        _logger.Info($"Scheduled task disabled: {taskName}");

        var quarantine = _store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult().ToList();
        quarantine.Add(new QuarantineEntry(
            Guid.NewGuid(),
            QuarantineEntryType.ScheduledTaskBackup,
            DateTimeOffset.Now,
            item.RiskLevel,
            item.Name,
            item.Publisher,
            taskName,
            taskName,
            null,
            null,
            null,
            "使用 schtasks /enable 重新启用"));
        _store.SaveAsync(quarantine, CancellationToken.None).GetAwaiter().GetResult();

        return new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "DisableTask", true, null);
    }

    private OperationRecord DisableStartup(ScanItem item)
    {
        var cleaner = new StartupCleaner(_logger, _store);
        return cleaner.DisableStartupAsync([item], CancellationToken.None).GetAwaiter().GetResult()[0];
    }

    private OperationRecord QuarantineFile(ScanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath))
        {
            throw new InvalidOperationException("目标路径为空");
        }

        if (!PathSafe.IsFilePathSafe(item.TargetPath))
        {
            throw new FileNotFoundException("目标文件不存在", item.TargetPath);
        }

        var parentDir = Path.GetDirectoryName(item.TargetPath);
        if (string.IsNullOrWhiteSpace(parentDir) || !PathSafe.IsUnderDirectory(item.TargetPath, parentDir))
        {
            throw new InvalidOperationException("路径校验失败，已阻止操作");
        }

        var quarantinePath = _store.CreateQuarantineFilePath(item.TargetPath);
        var fileInfo = new FileInfo(item.TargetPath);
        var sha256 = Sha256Hasher.HashFile(item.TargetPath);

        File.Move(item.TargetPath, quarantinePath);

        var quarantine = _store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult().ToList();
        quarantine.Add(new QuarantineEntry(
            Guid.NewGuid(),
            QuarantineEntryType.FileQuarantine,
            DateTimeOffset.Now,
            item.RiskLevel,
            item.Name,
            item.Publisher,
            item.TargetPath,
            quarantinePath,
            item.CommandLine,
            sha256,
            fileInfo.Length,
            "将文件从隔离区移动回原路径"));
        _store.SaveAsync(quarantine, CancellationToken.None).GetAwaiter().GetResult();

        _logger.Info($"File quarantined: {item.TargetPath}");
        return new OperationRecord(DateTimeOffset.Now, item.Id, item.Source, item.Name, "QuarantineFile", true, null);
    }

    private static string ExtractServiceName(ScanItem item)
    {
        var path = item.TargetPath;
        if (!string.IsNullOrWhiteSpace(path) && path.Contains('\\') && !path.Contains(' '))
        {
            return Path.GetFileName(path);
        }

        if (item.CommandLine is not null &&
            item.CommandLine.Contains("ServiceName", StringComparison.OrdinalIgnoreCase))
        {
            return item.Name;
        }

        return item.Name;
    }

    private void RunSchTasks(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit(15000);

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"schtasks 执行失败 (退出码 {process.ExitCode}): {error}");
        }
    }
}
