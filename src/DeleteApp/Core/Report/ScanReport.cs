using DeleteApp.Data.Models;

namespace DeleteApp.Core.Report;

public sealed record ScanReport(
    DateTimeOffset ScanTime,
    string MachineName,
    string UserName,
    int TotalCount,
    int HighRiskCount,
    int MediumRiskCount,
    int LowRiskCount,
    IReadOnlyList<ScanItem> Items
);
