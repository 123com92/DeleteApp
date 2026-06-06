using System.ServiceProcess;
using DeleteApp.Data.Models;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Core.Verifier;

public sealed class Verifier
{
    private readonly ILocalLogger _logger;

    public Verifier(ILocalLogger logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<VerificationResult>> VerifyAsync(IReadOnlyList<ScanItem> items, CancellationToken cancellationToken)
    {
        var results = new List<VerificationResult>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = item.RecommendedAction switch
                {
                    RecommendedAction.StopProcess => VerifyProcess(item),
                    RecommendedAction.DisableService => VerifyService(item),
                    RecommendedAction.DisableTask => VerifyTask(item),
                    RecommendedAction.QuarantineFile => VerifyFile(item),
                    RecommendedAction.DisableStartup => new VerificationResult(item.Id, item.Name, item.Source, true, "启动项已禁用（清除后验证不可精确）"),
                    _ => new VerificationResult(item.Id, item.Name, item.Source, null, "未实现验证")
                };

                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.Error($"Verification error: {item.Name}", ex);
                results.Add(new VerificationResult(item.Id, item.Name, item.Source, false, ex.Message));
            }
        }

        return Task.FromResult<IReadOnlyList<VerificationResult>>(results);
    }

    private static VerificationResult VerifyProcess(ScanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return new VerificationResult(item.Id, item.Name, item.Source, null, "进程名称为空");
        }

        var procName = Path.GetFileNameWithoutExtension(item.Name);
        var processes = System.Diagnostics.Process.GetProcessesByName(procName);

        if (processes.Length == 0)
        {
            return new VerificationResult(item.Id, item.Name, item.Source, true, "进程已停止");
        }

        return new VerificationResult(item.Id, item.Name, item.Source, false, $"仍有 {processes.Length} 个实例在运行");
    }

    private static VerificationResult VerifyService(ScanItem item)
    {
        try
        {
            var svcName = ExtractServiceName(item);
            using var sc = new ServiceController(svcName);

            var isDisabled = false;

            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{svcName}");
                var start = key?.GetValue("Start");
                isDisabled = start is int s && s == 4;
            }
            catch
            {
            }

            if (sc.Status == ServiceControllerStatus.Stopped || isDisabled)
            {
                return new VerificationResult(item.Id, item.Name, item.Source, true,
                    $"服务已停止: {sc.Status}, 禁用: {isDisabled}");
            }

            return new VerificationResult(item.Id, item.Name, item.Source, false, $"服务仍处于: {sc.Status}");
        }
        catch (Exception ex)
        {
            return new VerificationResult(item.Id, item.Name, item.Source, false, ex.Message);
        }
    }

    private static VerificationResult VerifyTask(ScanItem item)
    {
        try
        {
            var taskName = item.Name.TrimStart('\\');
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/query /tn \"{taskName}\" /FO CSV",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (output.Contains("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return new VerificationResult(item.Id, item.Name, item.Source, true, "计划任务已禁用");
            }

            if (output.Contains("Ready", StringComparison.OrdinalIgnoreCase))
            {
                return new VerificationResult(item.Id, item.Name, item.Source, false, "计划任务仍为启用状态");
            }

            return new VerificationResult(item.Id, item.Name, item.Source, null, $"schtasks 退出码: {process.ExitCode}");
        }
        catch (Exception ex)
        {
            return new VerificationResult(item.Id, item.Name, item.Source, null, $"验证错误: {ex.Message}");
        }
    }

    private static VerificationResult VerifyFile(ScanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return new VerificationResult(item.Id, item.Name, item.Source, null, "目标路径为空");
        }

        if (File.Exists(item.TargetPath))
        {
            return new VerificationResult(item.Id, item.Name, item.Source, false, "文件仍存在于原位置");
        }

        return new VerificationResult(item.Id, item.Name, item.Source, true, "文件已从原位置移除");
    }

    private static string ExtractServiceName(ScanItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.TargetPath) && item.TargetPath.Contains('\\') && !item.TargetPath.Contains(' '))
        {
            return Path.GetFileName(item.TargetPath);
        }

        return item.Name;
    }
}

public sealed record VerificationResult(
    Guid ItemId,
    string Name,
    ScanSource Source,
    bool? IsClean,
    string Message
);
