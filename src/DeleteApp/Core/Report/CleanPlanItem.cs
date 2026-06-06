using DeleteApp.Data.Models;

namespace DeleteApp.Core.Report;

public sealed record CleanPlanItem(
    Guid Id,
    ScanSource Source,
    string Name,
    string? Publisher,
    string? TargetPath,
    string? CommandLine,
    RiskLevel RiskLevel,
    IReadOnlyList<string> Reasons,
    RecommendedAction PlannedAction,
    bool IsRecoverable
);
