using System.Text.Json;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Core.Report;

public sealed class ReportWriter
{
    private readonly ILocalLogger _logger;

    public ReportWriter(ILocalLogger logger)
    {
        _logger = logger;
    }

    public async Task<string> WriteCleanPlanAsync(CleanPlanReport report, CancellationToken cancellationToken)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeleteApp",
            "reports");

        Directory.CreateDirectory(root);

        var fileName = $"clean_plan_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(root, fileName);

        try
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
                .ConfigureAwait(false);
            _logger.Info($"Clean plan exported: {path}");
        }
        catch (Exception ex)
        {
            _logger.Error("Export clean plan failed", ex);
            throw;
        }

        return path;
    }

    public async Task<string> WriteCleanResultAsync(CleanResultReport report, CancellationToken cancellationToken)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeleteApp",
            "reports");

        Directory.CreateDirectory(root);

        var fileName = $"clean_result_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(root, fileName);

        try
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
                .ConfigureAwait(false);
            _logger.Info($"Clean result exported: {path}");
        }
        catch (Exception ex)
        {
            _logger.Error("Export clean result failed", ex);
            throw;
        }

        return path;
    }

    public async Task<string> WriteScanReportAsync(ScanReport report, CancellationToken cancellationToken)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeleteApp",
            "reports");

        Directory.CreateDirectory(root);

        var fileName = $"scan_report_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(root, fileName);

        try
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
                .ConfigureAwait(false);
            _logger.Info($"Scan report exported: {path}");
        }
        catch (Exception ex)
        {
            _logger.Error("Export scan report failed", ex);
            throw;
        }

        return path;
    }
}
