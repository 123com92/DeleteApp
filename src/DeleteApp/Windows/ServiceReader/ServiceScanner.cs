using System.Management;
using System.ServiceProcess;
using DeleteApp.Core.Scanner;
using DeleteApp.Data.Models;
using DeleteApp.Utils;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Windows.ServiceReader;

public sealed class ServiceScanner : IScanner
{
    private readonly ILocalLogger _logger;

    public ServiceScanner(ILocalLogger logger)
    {
        _logger = logger;
    }

    public string Name => "ServiceScanner";

    public Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();

        try
        {
            var services = ServiceController.GetServices();
            foreach (var svc in services)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var path = TryGetServicePath(svc.ServiceName);
                    var isAutoStart = svc.StartType == ServiceStartMode.Automatic;

                    items.Add(new ScanCandidate(
                        GuidHelper.Deterministic("svc", svc.ServiceName),
                        $"svc:{svc.ServiceName}",
                        ScanSource.Service,
                        svc.DisplayName,
                        null,
                        path,
                        $"启动类型:{svc.StartType} 状态:{svc.Status}"
                    ));

                    _logger.Info($"Service scanned: {svc.ServiceName} start={svc.StartType} status={svc.Status}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Service scan error: {svc.ServiceName}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Service enumeration failed", ex);
        }

        return Task.FromResult<IReadOnlyList<ScanCandidate>>(items);
    }

    private string? TryGetServicePath(string serviceName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT PathName FROM Win32_Service WHERE Name = '{serviceName.Replace("'", "''")}'");
            foreach (var obj in searcher.Get())
            {
                var path = obj["PathName"]?.ToString();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path.Trim('"');
                }
            }
        }
        catch
        {
        }

        return null;
    }
}


