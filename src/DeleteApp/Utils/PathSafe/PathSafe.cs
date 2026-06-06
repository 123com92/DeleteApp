namespace DeleteApp.Utils.PathSafe;

public static class PathSafe
{
    public static bool IsUnderDirectory(string path, string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var fullDir = Path.GetFullPath(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsFilePathSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(path);
            return File.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
