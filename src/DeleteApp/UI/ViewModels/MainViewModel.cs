using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using DeleteApp.Core.Cleaner;
using DeleteApp.Core.Quarantine;
using DeleteApp.Core.Report;
using DeleteApp.Core.RiskEngine;
using DeleteApp.Core.Scanner;
using DeleteApp.Core.Verifier;
using DeleteApp.Data.Models;
using DeleteApp.UI.Commands;
using DeleteApp.Utils.Logger;
using DeleteApp.Windows.DesktopShortcutReader;
using DeleteApp.Windows.DirectoryReader;
using DeleteApp.Windows.InstalledProgramsReader;
using DeleteApp.Windows.ProcessReader;
using DeleteApp.Windows.ServiceReader;
using DeleteApp.Windows.StartupReader;
using DeleteApp.Windows.TaskSchedulerReader;

namespace DeleteApp.UI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ILocalLogger _logger;
    private readonly CompositeScanner _scanner;
    private readonly RiskEngine _riskEngine;
    private readonly ReportWriter _reportWriter;
    private readonly QuarantineStore _quarantineStore;
    private readonly GeneralCleaner _generalCleaner;
    private readonly Verifier _verifier;

    private CancellationTokenSource? _scanCts;
    private bool _isScanning;
    private bool _isOperating;
    private bool? _allSelected;
    private bool _onlyHighRisk;
    private int _activeTabIndex;
    private string _statusText = "就绪";
    private string _scanProgress = "";

    private List<ScanItemViewModel> _allItems = [];

    public MainViewModel()
    {
        _logger = LocalLogger.CreateDefault();
        _riskEngine = new RiskEngine();
        _reportWriter = new ReportWriter(_logger);
        _quarantineStore = new QuarantineStore(_logger);
        _generalCleaner = new GeneralCleaner(_logger, _quarantineStore);
        _verifier = new Verifier(_logger);

        _scanner = new CompositeScanner(
            new IScanner[]
            {
                new ProcessScanner(_logger),
                new StartupScanner(_logger),
                new ServiceScanner(_logger),
                new TaskSchedulerScanner(_logger),
                new InstalledProgramsScanner(_logger),
                new DesktopShortcutScanner(_logger),
                new DirectoryScanner(_logger)
            },
            _logger);

        AllItems = new ObservableCollection<ScanItemViewModel>();
        ProcessItems = new ObservableCollection<ScanItemViewModel>();
        StartupItems = new ObservableCollection<ScanItemViewModel>();
        ServiceItems = new ObservableCollection<ScanItemViewModel>();
        TaskItems = new ObservableCollection<ScanItemViewModel>();
        ProgramItems = new ObservableCollection<ScanItemViewModel>();
        ShortcutItems = new ObservableCollection<ScanItemViewModel>();
        QuarantineItems = new ObservableCollection<QuarantineEntryViewModel>();
        ReportFiles = new ObservableCollection<ReportFileViewModel>();

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => IsNotBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsScanning);
        ExecuteCleanCommand = new AsyncRelayCommand(ExecuteCleanAsync, () => CanExecuteClean);
        ExportPlanCommand = new AsyncRelayCommand(ExportPlanAsync, () => CanExportPlan);
        ExportReportCommand = new AsyncRelayCommand(ExportReportAsync, () => CanExportReport);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => CanRestore);
        RefreshReportsCommand = new AsyncRelayCommand(RefreshReportsAsync, () => IsNotBusy);

        _ = LoadQuarantineAsync();
        _ = RefreshReportsAsync();
    }

    public ObservableCollection<ScanItemViewModel> AllItems { get; }
    public ObservableCollection<ScanItemViewModel> ProcessItems { get; }
    public ObservableCollection<ScanItemViewModel> StartupItems { get; }
    public ObservableCollection<ScanItemViewModel> ServiceItems { get; }
    public ObservableCollection<ScanItemViewModel> TaskItems { get; }
    public ObservableCollection<ScanItemViewModel> ProgramItems { get; }
    public ObservableCollection<ScanItemViewModel> ShortcutItems { get; }
    public ObservableCollection<QuarantineEntryViewModel> QuarantineItems { get; }
    public ObservableCollection<ReportFileViewModel> ReportFiles { get; }

    public AsyncRelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand ExecuteCleanCommand { get; }
    public AsyncRelayCommand ExportPlanCommand { get; }
    public AsyncRelayCommand ExportReportCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }
    public AsyncRelayCommand RefreshReportsCommand { get; }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                RaiseBusyChanged();
            }
        }
    }

    public bool IsOperating
    {
        get => _isOperating;
        private set
        {
            if (SetProperty(ref _isOperating, value))
            {
                RaiseBusyChanged();
            }
        }
    }

    public bool IsBusy => IsScanning || IsOperating;
    public bool IsNotBusy => !IsBusy;

    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set
        {
            if (SetProperty(ref _activeTabIndex, value))
            {
                _allSelected = null;
                RaisePropertyChanged(nameof(AllSelected));
                RaisePropertyChanged(nameof(CanExecuteClean));
                ExecuteCleanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool? AllSelected
    {
        get => _allSelected;
        set
        {
            if (SetProperty(ref _allSelected, value) && value.HasValue)
            {
                foreach (var item in GetActiveTabItems())
                {
                    item.IsSelected = value.Value;
                }
            }
        }
    }

    public bool OnlyHighRisk
    {
        get => _onlyHighRisk;
        set
        {
            if (SetProperty(ref _onlyHighRisk, value))
            {
                foreach (var item in GetActiveTabItems())
                {
                    item.IsSelected = value && item.Item.RiskLevel == RiskLevel.High;
                }

                UpdateAllSummaries();
                RaiseCanExecutes();
            }
        }
    }

    public string ScanProgress
    {
        get => _scanProgress;
        private set => SetProperty(ref _scanProgress, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string AllSummary => $"全部 {AllItems.Count} | 高 {AllItems.Count(i => i.Item.RiskLevel == RiskLevel.High)} / 中 {AllItems.Count(i => i.Item.RiskLevel == RiskLevel.Medium)} / 低 {AllItems.Count(i => i.Item.RiskLevel == RiskLevel.Low)} | 已选 {AllItems.Count(i => i.IsSelected)}";
    public string ProcessSummary => $"进程 {ProcessItems.Count} | 高风险 {ProcessItems.Count(i => i.Item.RiskLevel == RiskLevel.High)} | 已选 {ProcessItems.Count(i => i.IsSelected)}";
    public string StartupSummary => $"启动项 {StartupItems.Count} | 高风险 {StartupItems.Count(i => i.Item.RiskLevel == RiskLevel.High)} | 已选 {StartupItems.Count(i => i.IsSelected)}";
    public string ServiceSummary => $"服务 {ServiceItems.Count} | 高风险 {ServiceItems.Count(i => i.Item.RiskLevel == RiskLevel.High)} | 已选 {ServiceItems.Count(i => i.IsSelected)}";
    public string TaskSummary => $"计划任务 {TaskItems.Count} | 高风险 {TaskItems.Count(i => i.Item.RiskLevel == RiskLevel.High)} | 已选 {TaskItems.Count(i => i.IsSelected)}";
    public string TotalSummaryText => $"总计 {AllItems.Count} | 隔离 {QuarantineItems.Count}";
    public string AdminStatusText => App.IsAdministrator ? "管理员" : "普通权限";

    public bool CanExportPlan => IsNotBusy && AllItems.Any(i => i.IsSelected);
    public bool CanExportReport => IsNotBusy && AllItems.Count > 0;
    public bool CanExecuteClean => IsNotBusy && GetActiveTabItems().Any(i => i.IsSelected && i.Item.RecommendedAction != RecommendedAction.None && i.Item.RecommendedAction != RecommendedAction.Review);
    public bool CanRestore => IsNotBusy && QuarantineItems.Any(i => i.IsSelected);

    private IEnumerable<ScanItemViewModel> GetActiveTabItems() => _activeTabIndex switch
    {
        0 => AllItems,
        1 => ProcessItems,
        2 => StartupItems,
        3 => ServiceItems,
        4 => TaskItems,
        5 => ProgramItems,
        6 => ShortcutItems,
        _ => AllItems
    };

    private void RaiseBusyChanged()
    {
        RaisePropertyChanged(nameof(IsBusy));
        RaisePropertyChanged(nameof(IsNotBusy));
        RaiseCanExecutes();
    }

    private void RaiseCanExecutes()
    {
        RaisePropertyChanged(nameof(CanExportPlan));
        RaisePropertyChanged(nameof(CanExportReport));
        RaisePropertyChanged(nameof(CanExecuteClean));
        RaisePropertyChanged(nameof(CanRestore));
        ScanCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        ExecuteCleanCommand.RaiseCanExecuteChanged();
        ExportPlanCommand.RaiseCanExecuteChanged();
        ExportReportCommand.RaiseCanExecuteChanged();
        RestoreCommand.RaiseCanExecuteChanged();
        RefreshReportsCommand.RaiseCanExecuteChanged();
    }

    private async Task ScanAsync()
    {
        Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsScanning = true;
        StatusText = "扫描中…";
        ScanProgress = "";

        ClearAllItems();
        UpdateAllSummaries();

        try
        {
            var scanned = await Task.Run(async () =>
            {
                var candidates = await _scanner.ScanAsync(token);
                return candidates.Select(c => _riskEngine.Evaluate(c)).ToList();
            }, token);

            var ordered = scanned.OrderByDescending(i => i.RiskLevel).ThenBy(i => i.Source).ThenBy(i => i.Name).ToList();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var item in ordered)
                {
                    var vm = new ScanItemViewModel(item);
                    vm.PropertyChanged += OnItemPropertyChanged;
                    _allItems.Add(vm);
                }

                AllItems.Clear();
                ProcessItems.Clear();
                StartupItems.Clear();
                ServiceItems.Clear();
                TaskItems.Clear();
                ProgramItems.Clear();
                ShortcutItems.Clear();

                foreach (var vm in _allItems)
                {
                    AllItems.Add(vm);
                    switch (vm.Item.Source)
                    {
                        case ScanSource.Process:
                            ProcessItems.Add(vm);
                            break;
                        case ScanSource.StartupRegistry:
                        case ScanSource.StartupFolder:
                            StartupItems.Add(vm);
                            break;
                        case ScanSource.Service:
                            ServiceItems.Add(vm);
                            break;
                        case ScanSource.ScheduledTask:
                            TaskItems.Add(vm);
                            break;
                        case ScanSource.InstalledProgram:
                            ProgramItems.Add(vm);
                            break;
                        case ScanSource.DesktopShortcut:
                            ShortcutItems.Add(vm);
                            break;
                    }
                }

                StatusText = $"扫描完成：{AllItems.Count} 项";
                ScanProgress = $"高 {AllItems.Count(i => i.Item.RiskLevel == RiskLevel.High)} / 中 {AllItems.Count(i => i.Item.RiskLevel == RiskLevel.Medium)} / 低 {AllItems.Count(i => i.Item.RiskLevel == RiskLevel.Low)}";
                UpdateAllSummaries();
                RaiseCanExecutes();
            });
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消扫描";
        }
        catch (Exception ex)
        {
            _logger.Error("Scan failed", ex);
            StatusText = $"扫描失败：{ex.GetType().Name}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void Cancel()
    {
        try { _scanCts?.Cancel(); }
        catch { }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private void ClearAllItems()
    {
        foreach (var vm in _allItems)
        {
            vm.PropertyChanged -= OnItemPropertyChanged;
        }

        _allItems.Clear();
        AllItems.Clear();
        ProcessItems.Clear();
        StartupItems.Clear();
        ServiceItems.Clear();
        TaskItems.Clear();
        ProgramItems.Clear();
        ShortcutItems.Clear();
    }

    private async Task ExecuteCleanAsync()
    {
        var selected = GetActiveTabItems().Where(i => i.IsSelected).Select(i => i.Item).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未选择任何项目";
            return;
        }

        var actionGroups = selected
            .GroupBy(i => i.RecommendedAction)
            .Select(g => $"{FormatAction(g.Key)}: {g.Count()} 项");

        var confirm = MessageBox.Show(
            $"将对 {selected.Count} 个项目执行清理：\n\n{string.Join("\n", actionGroups)}\n\n所有操作可恢复。继续？",
            "执行确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            StatusText = "已取消执行";
            return;
        }

        try
        {
            IsOperating = true;
            StatusText = "执行清理中…";

            var records = await Task.Run(() => _generalCleaner.ExecuteAsync(selected, CancellationToken.None));
            var results = await Task.Run(() => _verifier.VerifyAsync(selected, CancellationToken.None));

            var success = records.Count(r => r.Success);
            var failed = records.Count - success;

            var report = new CleanResultReport(
                DateTimeOffset.Now,
                Environment.MachineName,
                Environment.UserName,
                records.Count,
                success,
                failed,
                records);

            var path = await _reportWriter.WriteCleanResultAsync(report, CancellationToken.None);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText = $"清理完成：成功 {success} / 失败 {failed}（报告：{path}）";
            });

            await LoadQuarantineAsync();
            await ScanAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Clean failed", ex);
            StatusText = $"执行失败：{ex.GetType().Name}";
        }
        finally
        {
            IsOperating = false;
        }
    }

    private async Task ExportPlanAsync()
    {
        try
        {
            var selected = AllItems.Where(i => i.IsSelected).Select(i => i.Item).ToList();
            if (selected.Count == 0)
            {
                StatusText = "未选择任何项目";
                return;
            }

            var report = new CleanPlanReport(
                DateTimeOffset.Now,
                Environment.MachineName,
                Environment.UserName,
                selected.Count,
                selected.Select(i => new CleanPlanItem(
                    i.Id, i.Source, i.Name, i.Publisher, i.TargetPath, i.CommandLine,
                    i.RiskLevel, i.Reasons, i.RecommendedAction, i.IsRecoverable)).ToList());

            var path = await _reportWriter.WriteCleanPlanAsync(report, CancellationToken.None);
            StatusText = $"已导出清理计划：{path}";
        }
        catch (Exception ex)
        {
            _logger.Error("Export plan failed", ex);
            StatusText = $"导出失败：{ex.GetType().Name}";
        }
    }

    private async Task ExportReportAsync()
    {
        try
        {
            var scanReport = new ScanReport(
                DateTimeOffset.Now,
                Environment.MachineName,
                Environment.UserName,
                AllItems.Count,
                AllItems.Count(i => i.Item.RiskLevel == RiskLevel.High),
                AllItems.Count(i => i.Item.RiskLevel == RiskLevel.Medium),
                AllItems.Count(i => i.Item.RiskLevel == RiskLevel.Low),
                AllItems.Select(i => i.Item).ToList());

            var path = await _reportWriter.WriteScanReportAsync(scanReport, CancellationToken.None);
            StatusText = $"已导出扫描报告：{path}";
            await RefreshReportsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Export report failed", ex);
            StatusText = $"导出失败：{ex.GetType().Name}";
        }
    }

    private async Task RestoreAsync()
    {
        var selected = QuarantineItems.Where(i => i.IsSelected).Select(i => i.Entry).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未选择任何隔离项目";
            return;
        }

        var confirm = MessageBox.Show(
            $"将还原 {selected.Count} 个隔离项目。\n\n继续？",
            "还原确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            StatusText = "已取消还原";
            return;
        }

        try
        {
            IsOperating = true;
            StatusText = "还原中…";

            var startupCleaner = new StartupCleaner(_logger, _quarantineStore);
            var startupEntries = selected.Where(e => e.EntryType is QuarantineEntryType.StartupRegistryRunValue or QuarantineEntryType.StartupFolderFile).ToList();
            var records = await Task.Run(() => startupCleaner.RestoreAsync(startupEntries, CancellationToken.None));

            var nonStartup = selected.Count - startupEntries.Count;
            if (nonStartup > 0)
            {
                StatusText = $"还原完成：启动项已处理，{nonStartup} 项其他类型需手动恢复";
            }
            else
            {
                var success = records.Count(r => r.Success);
                var failed = records.Count - success;
                StatusText = $"还原完成：成功 {success} / 失败 {failed}";
            }

            await LoadQuarantineAsync();
            await ScanAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Restore failed", ex);
            StatusText = $"还原失败：{ex.GetType().Name}";
        }
        finally
        {
            IsOperating = false;
        }
    }

    private async Task LoadQuarantineAsync()
    {
        try
        {
            var entries = await _quarantineStore.LoadAsync(CancellationToken.None);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var vm in QuarantineItems.ToList())
                {
                    vm.PropertyChanged -= OnQuarantineItemPropertyChanged;
                }

                QuarantineItems.Clear();

                foreach (var entry in entries.OrderByDescending(e => e.QuarantineTime))
                {
                    var vm = new QuarantineEntryViewModel(entry);
                    vm.PropertyChanged += OnQuarantineItemPropertyChanged;
                    QuarantineItems.Add(vm);
                }

                RaisePropertyChanged(nameof(TotalSummaryText));
                RaisePropertyChanged(nameof(CanRestore));
                RestoreCommand.RaiseCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Load quarantine failed", ex);
        }
    }

    private async Task RefreshReportsAsync()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeleteApp",
                "reports");

            if (!Directory.Exists(root))
            {
                return;
            }

            var files = await Task.Run(() => Directory.GetFiles(root, "*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Take(100)
                .Select(f => new ReportFileViewModel(f.Name, f.Length, f.LastWriteTime, f.FullName))
                .ToList());

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ReportFiles.Clear();
                foreach (var f in files)
                {
                    ReportFiles.Add(f);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Refresh reports failed", ex);
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanItemViewModel.IsSelected))
        {
            UpdateAllSummaries();
            RaiseCanExecutes();
        }
    }

    private void OnQuarantineItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuarantineEntryViewModel.IsSelected))
        {
            RaisePropertyChanged(nameof(CanRestore));
            RestoreCommand.RaiseCanExecuteChanged();
        }
    }

    private void UpdateAllSummaries()
    {
        RaisePropertyChanged(nameof(AllSummary));
        RaisePropertyChanged(nameof(ProcessSummary));
        RaisePropertyChanged(nameof(StartupSummary));
        RaisePropertyChanged(nameof(ServiceSummary));
        RaisePropertyChanged(nameof(TaskSummary));
        RaisePropertyChanged(nameof(TotalSummaryText));
    }

    private static string FormatAction(RecommendedAction action) => action switch
    {
        RecommendedAction.StopProcess => "停止进程",
        RecommendedAction.DisableService => "禁用服务",
        RecommendedAction.DisableTask => "禁用任务",
        RecommendedAction.DisableStartup => "禁用启动项",
        RecommendedAction.QuarantineFile => "隔离文件",
        RecommendedAction.Uninstall => "建议卸载",
        _ => "待确认"
    };
}

public sealed record ReportFileViewModel(
    string FileName,
    long Size,
    DateTime LastModified,
    string FullPath
)
{
    public string SizeStr => Size switch
    {
        < 1024 => $"{Size} B",
        < 1024 * 1024 => $"{Size / 1024.0:F1} KB",
        _ => $"{Size / (1024.0 * 1024.0):F1} MB"
    };

    public string ModifiedStr => LastModified.ToString("yyyy-MM-dd HH:mm:ss");
}
