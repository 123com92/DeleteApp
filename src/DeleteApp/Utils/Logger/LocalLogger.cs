using System.Text;

namespace DeleteApp.Utils.Logger;

public sealed class LocalLogger : ILocalLogger
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _logFilePath;

    public LocalLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public static LocalLogger CreateDefault()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeleteApp",
            "logs");

        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "app.log");
        return new LocalLogger(filePath);
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var sb = new StringBuilder();
        sb.Append('[').Append(ts).Append("] ").Append(level).Append(' ').Append(message);

        if (exception is not null)
        {
            sb.AppendLine();
            sb.Append(exception);
        }

        sb.AppendLine();

        _ = WriteAsync(sb.ToString());
    }

    private async Task WriteAsync(string content)
    {
        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            await File.AppendAllTextAsync(_logFilePath, content).ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }
}
