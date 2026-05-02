using System.IO;

namespace DuctSupportAddin.Utilities;

/// <summary>
/// Simple logging utility.
/// </summary>
public static class Logger
{
    private static readonly string LogDirectory;
    private static readonly string LogFile;
    private static readonly object LockObj = new();
    
    static Logger()
    {
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AUS", "RectangularDuctSupport", "Logs");
        
        Directory.CreateDirectory(LogDirectory);
        
        LogFile = Path.Combine(LogDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
    }
    
    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message) => Log("ERROR", message);
    public static void Error(string message, Exception ex) => Log("ERROR", $"{message}: {ex.Message}\n{ex.StackTrace}");
    public static void Debug(string message) => Log("DEBUG", message);
    
    private static void Log(string level, string message)
    {
        try
        {
            lock (LockObj)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logLine = $"[{timestamp}] {level,-5} - {message}";
                File.AppendAllText(LogFile, logLine + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }
    
    /// <summary>
    /// Get log file path for support.
    /// </summary>
    public static string GetLogFilePath() => LogFile;
    
    /// <summary>
    /// Clean up old log files.
    /// </summary>
    public static void CleanOldLogs(int keepDays = 7)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            var files = Directory.GetFiles(LogDirectory, "log_*.txt");
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoff)
                {
                    fileInfo.Delete();
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// Progress reporting helper.
/// </summary>
public class ProgressManager : IProgress<string>
{
    private readonly Action<string>? _messageCallback;
    private readonly Action<int>? _percentCallback;
    private int _current;
    private int _total;
    
    public ProgressManager(Action<string>? messageCallback = null, Action<int>? percentCallback = null)
    {
        _messageCallback = messageCallback;
        _percentCallback = percentCallback;
    }
    
    public void SetTotal(int total)
    {
        _total = total;
        _current = 0;
    }
    
    public void Increment()
    {
        _current++;
        if (_total > 0)
        {
            int percent = (int)((double)_current / _total * 100);
            _percentCallback?.Invoke(percent);
        }
    }
    
    public void Report(string value)
    {
        _messageCallback?.Invoke(value);
    }
    
    public void Report(int current, int total, string message)
    {
        _current = current;
        _total = total;
        Report(message);
        if (total > 0)
        {
            int percent = (int)((double)current / total * 100);
            _percentCallback?.Invoke(percent);
        }
    }
}
