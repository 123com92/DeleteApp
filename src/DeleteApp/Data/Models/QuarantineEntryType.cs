namespace DeleteApp.Data.Models;

public enum QuarantineEntryType
{
    StartupRegistryRunValue = 0,
    StartupFolderFile = 1,
    ServiceConfigBackup = 2,
    ScheduledTaskBackup = 3,
    FileQuarantine = 4
}
