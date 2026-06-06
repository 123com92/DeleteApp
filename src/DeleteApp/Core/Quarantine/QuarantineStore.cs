using System.Text.Json;
using DeleteApp.Data.Models;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Core.Quarantine;

public sealed class QuarantineStore
{
    private readonly ILocalLogger _logger;
    private readonly string _rootDir;
    private readonly string _manifestPath;

    public QuarantineStore(ILocalLogger logger)
    {
        _logger = logger;
        _rootDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeleteApp",
            "quarantine");
        _manifestPath = Path.Combine(_rootDir, "manifest.json");
        Directory.CreateDirectory(_rootDir);
    }

    public string RootDirectory => _rootDir;

    public async Task<IReadOnlyList<QuarantineEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_manifestPath))
            {
                return Array.Empty<QuarantineEntry>();
            }

            await using var stream = File.OpenRead(_manifestPath);
            var entries = await JsonSerializer.DeserializeAsync<List<QuarantineEntry>>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return entries ?? [];
        }
        catch (Exception ex)
        {
            _logger.Error("Load quarantine manifest failed", ex);
            return Array.Empty<QuarantineEntry>();
        }
    }

    public async Task SaveAsync(IReadOnlyList<QuarantineEntry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootDir);

        var tempPath = _manifestPath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, entries, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Copy(tempPath, _manifestPath, overwrite: true);
        File.Delete(tempPath);
    }

    public string CreateQuarantineFilePath(string originalFilePath)
    {
        var safeName = Path.GetFileName(originalFilePath);
        var unique = $"{DateTimeOffset.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{safeName}";
        return Path.Combine(_rootDir, unique);
    }
}
