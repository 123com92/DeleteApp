namespace DeleteApp.Data.Models;

public sealed record ScanItem(
    Guid Id,
    ScanSource Source,
    string Name,
    string? Publisher,
    string? TargetPath,
    string? CommandLine,
    RiskLevel RiskLevel,
    IReadOnlyList<string> Reasons,
    RecommendedAction RecommendedAction,
    bool IsRecoverable
);
