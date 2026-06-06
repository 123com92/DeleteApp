using DeleteApp.Data.Models;

namespace DeleteApp.UI.ViewModels;

public sealed class QuarantineEntryViewModel : ObservableObject
{
    private bool _isSelected;

    public QuarantineEntryViewModel(QuarantineEntry entry)
    {
        Entry = entry;
    }

    public QuarantineEntry Entry { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string RiskLevel => Entry.RiskLevel switch
    {
        Data.Models.RiskLevel.High => "高风险",
        Data.Models.RiskLevel.Medium => "中风险",
        _ => "低风险"
    };

    public string Type => Entry.EntryType switch
    {
        QuarantineEntryType.StartupRegistryRunValue => "注册表启动项",
        QuarantineEntryType.StartupFolderFile => "启动文件夹",
        _ => Entry.EntryType.ToString()
    };

    public string Name => Entry.Name;

    public string Publisher => Entry.Publisher ?? "";

    public string OriginalLocation => Entry.OriginalLocation;

    public string QuarantineLocation => Entry.QuarantineLocation;

    public string QuarantineTime => Entry.QuarantineTime.ToString("yyyy-MM-dd HH:mm:ss");

    public string RestoreHint => Entry.RestoreHint;
}
