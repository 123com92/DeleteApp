using DeleteApp.Core.Scanner;
using DeleteApp.Data.Models;
using DeleteApp.Utils;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Windows.DesktopShortcutReader;

public sealed class DesktopShortcutScanner : IScanner
{
    private readonly ILocalLogger _logger;

    private static readonly string[] ScanDirs;

    static DesktopShortcutScanner()
    {
        var dirs = new List<string>();
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop))
        {
            dirs.Add(desktop);
        }

        var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (!string.IsNullOrWhiteSpace(commonDesktop) && !string.Equals(commonDesktop, desktop, StringComparison.OrdinalIgnoreCase))
        {
            dirs.Add(commonDesktop);
        }

        ScanDirs = dirs.ToArray();
    }

    public DesktopShortcutScanner(ILocalLogger logger)
    {
        _logger = logger;
    }

    public string Name => "DesktopShortcutScanner";

    public Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();

        foreach (var dir in ScanDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(file);

                        items.Add(new ScanCandidate(
                            GuidHelper.Deterministic("lnk", file),
                            $"lnk:{file}",
                            ScanSource.DesktopShortcut,
                            name,
                            null,
                            file,
                            "桌面快捷方式"
                        ));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Desktop shortcut scan error: {file}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Desktop shortcut directory scan error: {dir}", ex);
            }
        }

        return Task.FromResult<IReadOnlyList<ScanCandidate>>(items);
    }
}
