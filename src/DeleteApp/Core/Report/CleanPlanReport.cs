namespace DeleteApp.Core.Report;

public sealed record CleanPlanReport(
    DateTimeOffset CreatedTime,
    string MachineName,
    string UserName,
    int SelectedCount,
    IReadOnlyList<CleanPlanItem> Items
);
