namespace DeleteApp.Data.Models;

public enum RecommendedAction
{
    None = 0,
    Review = 1,
    DisableStartup = 2,
    StopProcess = 3,
    QuarantineFile = 4,
    DisableService = 5,
    DisableTask = 6,
    Uninstall = 7
}
