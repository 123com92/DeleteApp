using DeleteApp.Data.Models;
using DeleteApp.Utils.Logger;

namespace DeleteApp.Core.Scanner;

public sealed class CompositeScanner : IScanner
{
    private readonly IReadOnlyList<IScanner> _scanners;
    private readonly ILocalLogger _logger;

    public CompositeScanner(IReadOnlyList<IScanner> scanners, ILocalLogger logger)
    {
        _scanners = scanners;
        _logger = logger;
    }

    public string Name => "Composite";

    public async Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var items = new List<ScanCandidate>();

        foreach (var scanner in _scanners)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await scanner.ScanAsync(cancellationToken).ConfigureAwait(false);
                items.AddRange(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Scanner failed: {scanner.Name}", ex);
            }
        }

        return items;
    }
}
