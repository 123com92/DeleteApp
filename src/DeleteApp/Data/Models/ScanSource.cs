namespace DeleteApp.Data.Models;

public enum ScanSource
{
    Process = 0,
    StartupRegistry = 1,
    StartupFolder = 2,
    Service = 3,
    ScheduledTask = 4,
    InstalledProgram = 5,
    DesktopShortcut = 6,
    DirectoryScan = 7
}
