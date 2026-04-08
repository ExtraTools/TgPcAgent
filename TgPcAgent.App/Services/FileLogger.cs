using System.Text;

namespace TgPcAgent.App.Services;

public sealed class FileLogger
{
    private readonly object _sync = new();
    private readonly string _logDirectory;

    public FileLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public string CurrentLogPath => Path.Combine(_logDirectory, $"agent-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        var builder = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append(" [")
            .Append(level)
            .Append("] ")
            .AppendLine(message);

        if (exception is not null)
        {
            builder.AppendLine(exception.ToString());
        }

        lock (_sync)
        {
            File.AppendAllText(CurrentLogPath, builder.ToString(), Encoding.UTF8);
        }
    }
}
