using Microsoft.Win32;
using DeleteApp.Core.Scanner;
using DeleteApp.Data.Models;
using DeleteApp.Utils;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Windows.InstalledProgramsReader;

public sealed class InstalledProgramsScanner : IScanner
{
    private readonly ILocalLogger _logger;

    private static readonly string[] RegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    public InstalledProgramsScanner(ILocalLogger logger)
    {
        _logger = logger;
    }

    public string Name => "InstalledProgramsScanner";

    public Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();

        foreach (var basePath in RegistryPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var rootKey = Registry.LocalMachine.OpenSubKey(basePath);
                if (rootKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in rootKey.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var subKey = rootKey.OpenSubKey(subKeyName);
                        if (subKey is null)
                        {
                            continue;
                        }

                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            continue;
                        }

                        var publisher = subKey.GetValue("Publisher") as string;
                        var installLocation = subKey.GetValue("InstallLocation") as string;
                        var uninstallString = subKey.GetValue("UninstallString") as string;

                        items.Add(new ScanCandidate(
                            GuidHelper.Deterministic("inst", displayName),
                            $"inst:{displayName}",
                            ScanSource.InstalledProgram,
                            displayName,
                            publisher,
                            installLocation,
                            uninstallString
                        ));

                        _logger.Info($"Installed program scanned: {displayName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Installed program scan error: {subKeyName}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Installed program registry path error: {basePath}", ex);
            }
        }

        return Task.FromResult<IReadOnlyList<ScanCandidate>>(items);
    }
}
