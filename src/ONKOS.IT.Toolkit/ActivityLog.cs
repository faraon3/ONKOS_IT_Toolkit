using System;
using System.IO;
using System.Text;

namespace ONKOS.IT.Toolkit;

internal static class ActivityLog
{
    private static readonly object _lock = new();
    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "Data", "Reports", "activity.log");

    public static void Write(string module, string action, string result, string? detail = null)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.Append(" | ");
                sb.Append(Environment.UserName.PadRight(20));
                sb.Append(" | ");
                sb.Append(Environment.MachineName.PadRight(15));
                sb.Append(" | ");
                sb.Append(module.PadRight(10));
                sb.Append(" | ");
                sb.Append(action.PadRight(30));
                sb.Append(" | ");
                sb.Append(result);
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    sb.Append(" | ");
                    sb.Append(detail.Replace("\r", " ").Replace("\n", " "));
                }
                sb.AppendLine();
                File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch { /* auditoría no debe romper la operación principal */ }
    }

    public static string GetLogPath() => LogPath;
}
