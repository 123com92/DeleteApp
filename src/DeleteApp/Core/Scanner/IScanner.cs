using DeleteApp.Data.Models;

namespace DeleteApp.Core.Scanner;

public interface IScanner
{
    string Name { get; }
    Task<IReadOnlyList<ScanCandidate>> ScanAsync(CancellationToken cancellationToken);
}
