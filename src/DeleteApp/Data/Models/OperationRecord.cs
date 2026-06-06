namespace DeleteApp.Data.Models;

public sealed record OperationRecord(
    DateTimeOffset Time,
    Guid ScanItemId,
    ScanSource Source,
    string Name,
    string Operation,
    bool Success,
    string? ErrorMessage
);
