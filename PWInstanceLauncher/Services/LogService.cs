namespace PWInstanceLauncher.Services
{
    internal class LogService
    {
        private readonly string _logPath;
        private readonly object _syncRoot = new();

        public LogService()
        {
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            _logPath = Path.Combine(logDirectory, "app.log");
        }

        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message, Exception? ex = null)
        {
            var text = ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}";
            Write("ERROR", text);
        }

        private void Write(string level, string message)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (_syncRoot)
            {
                File.AppendAllText(_logPath, line);
            }
        }
    }
}
