using DeleteApp.Data.Models;

namespace DeleteApp.Core.RiskEngine;

public sealed class RiskEngine
{
    private static readonly string[] SuspiciousNameTokens =
    [
        "ad", "ads", "advert", "popup", "wallpaper", "toolbar", "hijack",
        "coupon", "gamecenter", "gamebox", "minigame", "newscenter",
        "weather", "helper", "assistant", "speedup", "cleaner", "optimizer",
        "master", "guard", "safe", "protect"
    ];

    private static readonly string[] HighRiskNameTokens =
    [
        "adware", "spyware", "malware", "trojan", "ransom", "crack",
        "keygen", "inject", "hook", "keylogger"
    ];

    private static readonly string[] SuspiciousPublisherTokens =
    [
        "tianji", "sogou", "baidu", "360", "tencent", "kingsoft",
        "qihoo", "iobit", "auslogics", "wisecare", "glary",
        "driver booster", "driver easy", "avg", "avast", "norton",
        "mcafee", "kaspersky", "bitdefender"
    ];

    private static readonly string[] SystemPublisherTokens =
    ["microsoft", "intel", "amd", "nvidia", "realtek", "dell", "hp", "lenovo"];

    public ScanItem Evaluate(ScanCandidate candidate)
    {
        var reasons = new List<string>();
        var risk = RiskLevel.Low;

        var targetPath = candidate.TargetPath;
        var publisher = candidate.Publisher;
        var lowerName = candidate.Name.ToLowerInvariant();
        var lowerPublisher = publisher?.ToLowerInvariant() ?? "";

        EvalPath(candidate, targetPath, reasons, ref risk);
        EvalPublisher(lowerPublisher, reasons, ref risk);
        EvalName(lowerName, reasons, ref risk);
        EvalSource(candidate, reasons, ref risk);

        if (risk == RiskLevel.Low && reasons.Count == 0)
        {
            reasons.Add("暂无足够证据，建议人工确认");
        }

        var action = DetermineAction(candidate, risk);

        return new ScanItem(
            candidate.Id,
            candidate.Source,
            candidate.Name,
            candidate.Publisher,
            candidate.TargetPath,
            candidate.CommandLine,
            risk,
            reasons,
            action,
            IsRecoverable: action is not RecommendedAction.None);
    }

    private void EvalPath(ScanCandidate candidate, string? path, List<string> reasons, ref RiskLevel risk)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (candidate.Source != ScanSource.DesktopShortcut)
            {
                reasons.Add("无法获取目标路径");
                risk = Max(risk, RiskLevel.Medium);
            }
            return;
        }

        var normalized = path.Replace('/', '\\');

        if (normalized.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("位于 System32（通常为系统组件）");
        }

        if (normalized.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("位于 Temp 目录（高风险位置）");
            risk = Max(risk, RiskLevel.High);
        }

        if (normalized.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase))
        {
            if (normalized.Contains(@"\Roaming\", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("位于 AppData\\Roaming（常驻/自动更新程序常见位置）");
                risk = Max(risk, RiskLevel.Medium);
            }
            else
            {
                reasons.Add("位于 AppData");
                risk = Max(risk, RiskLevel.Medium);
            }
        }

        if (normalized.Contains(@"\Program Files (x86)\", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\Program Files\", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("位于 Program Files（已安装程序目录）");
        }
    }

    private void EvalPublisher(string lowerPublisher, List<string> reasons, ref RiskLevel risk)
    {
        if (string.IsNullOrWhiteSpace(lowerPublisher))
        {
            reasons.Add("未获取到厂商/发布者信息");
            risk = Max(risk, RiskLevel.Medium);
            return;
        }

        var isSystem = false;
        foreach (var token in SystemPublisherTokens)
        {
            if (lowerPublisher.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                isSystem = true;
                break;
            }
        }

        if (isSystem)
        {
            reasons.Add("厂商为知名系统/硬件厂商（降低风险）");
            return;
        }

        foreach (var token in SuspiciousPublisherTokens)
        {
            if (lowerPublisher.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"发布者包含可疑关键词：{token}");
                risk = Max(risk, RiskLevel.Medium);
                break;
            }
        }
    }

    private void EvalName(string lowerName, List<string> reasons, ref RiskLevel risk)
    {
        foreach (var token in HighRiskNameTokens)
        {
            if (lowerName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"名称包含高风险关键词：{token}");
                risk = Max(risk, RiskLevel.High);
                return;
            }
        }

        foreach (var token in SuspiciousNameTokens)
        {
            if (lowerName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"名称包含可疑关键词：{token}");
                risk = Max(risk, RiskLevel.Medium);
                break;
            }
        }
    }

    private void EvalSource(ScanCandidate candidate, List<string> reasons, ref RiskLevel risk)
    {
        switch (candidate.Source)
        {
            case ScanSource.StartupRegistry:
            case ScanSource.StartupFolder:
                reasons.Add("开机自启动项");
                risk = Max(risk, RiskLevel.Medium);
                break;

            case ScanSource.Service:
                if (candidate.CommandLine is not null &&
                    candidate.CommandLine.Contains("Automatic", StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("自动启动的服务");
                    risk = Max(risk, RiskLevel.Medium);
                }
                break;

            case ScanSource.ScheduledTask:
                reasons.Add("计划任务");
                risk = Max(risk, RiskLevel.Medium);
                break;

            case ScanSource.DesktopShortcut:
                reasons.Add("桌面快捷方式");
                break;

            case ScanSource.InstalledProgram:
                reasons.Add("已安装程序");
                break;

            case ScanSource.DirectoryScan:
                reasons.Add("目录扫描发现");
                break;
        }
    }

    private static RecommendedAction DetermineAction(ScanCandidate candidate, RiskLevel risk)
    {
        if (risk == RiskLevel.High)
        {
            return candidate.Source switch
            {
                ScanSource.Process => RecommendedAction.StopProcess,
                ScanSource.Service => RecommendedAction.DisableService,
                ScanSource.ScheduledTask => RecommendedAction.DisableTask,
                ScanSource.StartupRegistry or ScanSource.StartupFolder => RecommendedAction.DisableStartup,
                _ => RecommendedAction.QuarantineFile
            };
        }

        return candidate.Source switch
        {
            ScanSource.StartupRegistry or ScanSource.StartupFolder => RecommendedAction.DisableStartup,
            ScanSource.Service => RecommendedAction.DisableService,
            ScanSource.ScheduledTask => RecommendedAction.DisableTask,
            ScanSource.Process => RecommendedAction.StopProcess,
            ScanSource.InstalledProgram => RecommendedAction.Uninstall,
            _ => RecommendedAction.Review
        };
    }

    private static RiskLevel Max(RiskLevel a, RiskLevel b) => a > b ? a : b;
}
