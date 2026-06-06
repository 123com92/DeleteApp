using DeleteApp.Data.Models;

namespace DeleteApp.Core.Report;

public sealed record CleanResultReport(
    DateTimeOffset Time,
    string MachineName,
    string UserName,
    int RequestedCount,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<OperationRecord> Operations
);
