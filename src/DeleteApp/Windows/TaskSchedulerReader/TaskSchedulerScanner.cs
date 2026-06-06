using System.Diagnostics;
using DeleteApp.Core.Scanner;
using DeleteApp.Data.Models;
using DeleteApp.Utils;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Windows.TaskSchedulerReader;

public sealed class TaskSchedulerScanner : IScanner
{
    private readonly ILocalLogger _logger;

    public TaskSchedulerScanner(ILocalLogger logger)
    {
        _logger = logger;
    }

    public string Name => "TaskSchedulerScanner";

    public Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/query /FO CSV /V",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            var started = false;
            string[]? header = null;

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var trimmed = line.Trim();
                if (!started)
                {
                    header = trimmed.Split(',');
                    started = true;
                    continue;
                }

                var parts = ParseCsvLine(trimmed);
                if (parts.Length < 3)
                {
                    continue;
                }

                var taskName = parts.Length > 1 ? parts[1].Trim('"') : "";
                var author = parts.Length > 10 ? parts[10].Trim('"') : null;
                var appPath = parts.Length > 17 ? parts[17].Trim('"') : null;
                var scheduleType = parts.Length > 16 ? parts[16].Trim('"') : null;

                if (string.IsNullOrWhiteSpace(taskName) || taskName == "\\")
                {
                    continue;
                }

                items.Add(new ScanCandidate(
                    GuidHelper.Deterministic("tsk", taskName),
                    $"tsk:{taskName}",
                    ScanSource.ScheduledTask,
                    taskName,
                    author,
                    appPath,
                    scheduleType
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Task scheduler enumeration failed", ex);
        }

        return Task.FromResult<IReadOnlyList<ScanCandidate>>(items);
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = "";

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current += '"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }

        result.Add(current);
        return result.ToArray();
    }
}
