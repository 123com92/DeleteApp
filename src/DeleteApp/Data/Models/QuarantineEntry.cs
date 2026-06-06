namespace DeleteApp.Data.Models;

public sealed record QuarantineEntry(
    Guid Id,
    QuarantineEntryType EntryType,
    DateTimeOffset QuarantineTime,
    RiskLevel RiskLevel,
    string Name,
    string? Publisher,
    string OriginalLocation,
    string QuarantineLocation,
    string? CommandLine,
    string? FileSha256,
    long? FileSize,
    string RestoreHint
);
