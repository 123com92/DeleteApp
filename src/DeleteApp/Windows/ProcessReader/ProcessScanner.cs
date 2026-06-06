using System.Diagnostics;
using DeleteApp.Core.Scanner;
using DeleteApp.Data.Models;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Windows.ProcessReader;

public sealed class ProcessScanner : IScanner
{
    private readonly ILocalLogger _logger;

    public ProcessScanner(ILocalLogger logger)
    {
        _logger = logger;
    }

    public string Name => "Process";

    public Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();

        foreach (var proc in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string? path = null;
                try
                {
                    path = proc.MainModule?.FileName;
                }
                catch
                {
                }

                string? publisher = null;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        publisher = FileVersionInfo.GetVersionInfo(path).CompanyName;
                    }
                    catch
                    {
                    }
                }

                items.Add(new ScanCandidate(
                    Guid.NewGuid(),
                    $"process:{proc.Id}",
                    ScanSource.Process,
                    proc.ProcessName,
                    string.IsNullOrWhiteSpace(publisher) ? null : publisher,
                    path,
                    null));
            }
            catch (Exception ex)
            {
                _logger.Warn($"Process scan item failed: {proc.ProcessName}. {ex.GetType().Name}");
            }
            finally
            {
                proc.Dispose();
            }
        }

        return Task.FromResult<IReadOnlyList<ScanCandidate>>(items);
    }
}
