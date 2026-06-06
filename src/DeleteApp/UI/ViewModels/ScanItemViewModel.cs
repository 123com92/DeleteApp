using DeleteApp.Data.Models;

namespace DeleteApp.UI.ViewModels;

public sealed class ScanItemViewModel : ObservableObject
{
    private bool _isSelected;

    public ScanItemViewModel(ScanItem item)
    {
        Item = item;
    }

    public ScanItem Item { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string RiskLevel => Item.RiskLevel switch
    {
        Data.Models.RiskLevel.High => "高风险",
        Data.Models.RiskLevel.Medium => "中风险",
        _ => "低风险"
    };

    public string Source => Item.Source switch
    {
        ScanSource.Process => "进程",
        ScanSource.StartupRegistry => "注册表启动",
        ScanSource.StartupFolder => "启动文件夹",
        ScanSource.Service => "服务",
        ScanSource.ScheduledTask => "计划任务",
        ScanSource.InstalledProgram => "安装程序",
        ScanSource.DesktopShortcut => "快捷方式",
        ScanSource.DirectoryScan => "目录扫描",
        _ => Item.Source.ToString()
    };

    public string Name => Item.Name;

    public string Publisher => Item.Publisher ?? "";

    public string TargetPath => Item.TargetPath ?? "";

    public string CommandLine => Item.CommandLine ?? "";

    public string Reasons => string.Join("；", Item.Reasons);

    public string Action => Item.RecommendedAction switch
    {
        RecommendedAction.Review => "待确认",
        RecommendedAction.DisableStartup => "禁用启动",
        RecommendedAction.StopProcess => "停止进程",
        RecommendedAction.QuarantineFile => "隔离文件",
        RecommendedAction.DisableService => "禁用服务",
        RecommendedAction.DisableTask => "禁用任务",
        RecommendedAction.Uninstall => "卸载",
        _ => "无"
    };

    public string IsRecoverableStr => Item.IsRecoverable ? "可恢复" : "不可恢复";
}
