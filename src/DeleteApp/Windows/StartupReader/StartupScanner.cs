using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using DeleteApp.Core.Scanner;
using DeleteApp.Data.Models;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Windows.StartupReader;

public sealed class StartupScanner : IScanner
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly ILocalLogger _logger;

    public StartupScanner(ILocalLogger logger)
    {
        _logger = logger;
    }

    public string Name => "Startup";

    public Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();
        ScanRegistry(items, cancellationToken);
        ScanStartupFolder(items, cancellationToken);
        return Task.FromResult<IReadOnlyList<ScanCandidate>>(items);
    }

    private void ScanRegistry(List<ScanCandidate> items, CancellationToken cancellationToken)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key is null)
            {
                return;
            }

            foreach (var valueName in key.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? commandLine = null;
                try
                {
                    commandLine = key.GetValue(valueName) as string;
                }
                catch
                {
                }

                var targetPath = TryExtractExecutablePath(commandLine);
                var publisher = TryGetPublisher(targetPath);

                items.Add(new ScanCandidate(
                    Guid.NewGuid(),
                    $@"startup:HKCU\{RunKeyPath}\{valueName}",
                    ScanSource.StartupRegistry,
                    valueName,
                    publisher,
                    targetPath,
                    commandLine));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Startup registry scan failed", ex);
        }
    }

    private void ScanStartupFolder(List<ScanCandidate> items, CancellationToken cancellationToken)
    {
        try
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (string.IsNullOrWhiteSpace(startupFolder) || !Directory.Exists(startupFolder))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(startupFolder))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ext = Path.GetExtension(file);
                string? targetPath = null;
                string? commandLine = null;

                if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    (targetPath, commandLine) = TryResolveShortcut(file);
                }
                else
                {
                    targetPath = file;
                }

                var publisher = TryGetPublisher(targetPath);

                items.Add(new ScanCandidate(
                    Guid.NewGuid(),
                    $"startup:{file}",
                    ScanSource.StartupFolder,
                    Path.GetFileName(file),
                    publisher,
                    targetPath,
                    commandLine));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Startup folder scan failed", ex);
        }
    }

    private static string? TryExtractExecutablePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var text = commandLine.Trim();

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 1)
            {
                return text.Substring(1, end - 1);
            }

            return null;
        }

        var firstSpace = text.IndexOf(' ');
        if (firstSpace <= 0)
        {
            return text;
        }

        return text.Substring(0, firstSpace);
    }

    private static string? TryGetPublisher(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        try
        {
            if (!File.Exists(targetPath))
            {
                return null;
            }

            var company = FileVersionInfo.GetVersionInfo(targetPath).CompanyName;
            return string.IsNullOrWhiteSpace(company) ? null : company;
        }
        catch
        {
            return null;
        }
    }

    private (string? targetPath, string? commandLine) TryResolveShortcut(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return (shortcutPath, null);
            }

            dynamic? shell = null;
            dynamic? shortcut = null;

            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shell?.CreateShortcut(shortcutPath);

                string? target = null;
                string? args = null;

                try
                {
                    target = shortcut?.TargetPath as string;
                }
                catch
                {
                }

                try
                {
                    args = shortcut?.Arguments as string;
                }
                catch
                {
                }

                string? cmd = null;
                if (!string.IsNullOrWhiteSpace(args))
                {
                    cmd = args;
                }

                if (string.IsNullOrWhiteSpace(target))
                {
                    return (shortcutPath, cmd);
                }

                return (target, cmd);
            }
            finally
            {
                if (shortcut is not null)
                {
                    try
                    {
                        Marshal.FinalReleaseComObject(shortcut);
                    }
                    catch
                    {
                    }
                }

                if (shell is not null)
                {
                    try
                    {
                        Marshal.FinalReleaseComObject(shell);
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Shortcut resolve failed: {shortcutPath}. {ex.GetType().Name}");
            return (shortcutPath, null);
        }
    }
}
