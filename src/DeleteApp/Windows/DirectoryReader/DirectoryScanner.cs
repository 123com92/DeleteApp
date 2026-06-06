using DeleteApp.Core.Scanner;
using DeleteApp.Data.Models;
using DeleteApp.Utils;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Windows.DirectoryReader;

public sealed class DirectoryScanner : IScanner
{
    private readonly ILocalLogger _logger;

    private static readonly (string tag, string path)[] DirectoryList;

    static DirectoryScanner()
    {
        DirectoryList = new (string, string)[]
        {
            ("AppData/Local", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            ("AppData/Roaming", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            ("ProgramFiles", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
            ("ProgramFilesX86", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)),
            ("Temp", Path.GetTempPath())
        };
    }

    public DirectoryScanner(ILocalLogger logger)
    {
        _logger = logger;
    }

    public string Name => "DirectoryScanner";

    public Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();

        foreach (var (tag, dirPath) in DirectoryList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var path = dirPath;
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    continue;
                }

                var topDirs = TryGetDirectories(path);
                foreach (var dir in topDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var dirName = Path.GetFileName(dir);
                    items.Add(new ScanCandidate(
                        GuidHelper.Deterministic("dir", dir),
                        $"dir:{dir}",
                        ScanSource.DirectoryScan,
                        dirName,
                        null,
                        dir,
                        $"位置:{tag}"
                    ));

                    _logger.Info($"Directory scanned: {dir}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Directory scan error: {tag}", ex);
            }
        }

        return Task.FromResult<IReadOnlyList<ScanCandidate>>(items);
    }

    private static string[] TryGetDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch
        {
            return [];
        }
    }
}
