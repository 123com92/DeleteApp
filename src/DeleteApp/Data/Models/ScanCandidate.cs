namespace DeleteApp.Data.Models;

public sealed record ScanCandidate(
    Guid Id,
    string UniqueKey,
    ScanSource Source,
    string Name,
    string? Publisher,
    string? TargetPath,
    string? CommandLine
);
