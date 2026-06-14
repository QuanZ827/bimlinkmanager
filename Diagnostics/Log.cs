using System;
using System.IO;
using System.Text;

namespace BimLinkManager.Diagnostics
{
    internal static class Log
    {
        private static readonly object _gate = new object();

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", message, ex);
        }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                AppConstants.EnsureAppDataFolder();
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(" [").Append(level).Append("] ");
                sb.Append(message);
                if (ex != null)
                {
                    var depth = 0;
                    var cur = ex;
                    while (cur != null && depth < 10)
                    {
                        sb.AppendLine();
                        sb.Append("    [")
                          .Append(depth == 0 ? "EX" : "INNER+" + depth)
                          .Append("] ")
                          .Append(cur.GetType().FullName).Append(": ")
                          .Append(cur.Message);
                        cur = cur.InnerException;
                        depth++;
                    }
                    if (!string.IsNullOrEmpty(ex.StackTrace))
                    {
                        sb.AppendLine();
                        sb.Append(ex.StackTrace);
                    }
                }
                sb.AppendLine();

                lock (_gate)
                {
                    File.AppendAllText(AppConstants.LogFilePath, sb.ToString());
                }
            }
            catch
            {
            }
        }
    }
}
