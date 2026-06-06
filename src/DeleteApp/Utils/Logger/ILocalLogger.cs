namespace DeleteApp.Utils.Logger;

public interface ILocalLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}
