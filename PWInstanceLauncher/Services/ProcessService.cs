using System.Diagnostics;
using System.IO;
using System.Management;

namespace PWInstanceLauncher.Services
{
    internal class ProcessService : IProcessService
    {
        private const string ProcessName = "elementclient";
        private static readonly TimeSpan CommandLineCacheTtl = TimeSpan.FromSeconds(5);

        private readonly Dictionary<string, int> _cachedPidByLogin = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, CommandLineCacheEntry> _commandLineByPid = new();

        public IGameProcess? TryFindRunningByLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return null;
            }

            if (_cachedPidByLogin.TryGetValue(login, out var cachedPid))
            {
                var cachedProcess = TryResolveProcess(cachedPid, login);
                if (cachedProcess is not null)
                {
                    return cachedProcess;
                }

                _cachedPidByLogin.Remove(login);
            }

            foreach (var process in Process.GetProcessesByName(ProcessName))
            {
                var commandLine = GetProcessCommandLine(process.Id);
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    continue;
                }

                if (CommandLineContainsLogin(commandLine, login))
                {
                    _cachedPidByLogin[login] = process.Id;
                    return new SystemGameProcess(process);
                }
            }

            return null;
        }

        private IGameProcess? TryResolveProcess(int processId, string login)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return null;
                }

                var commandLine = GetProcessCommandLine(processId);
                return !string.IsNullOrWhiteSpace(commandLine) && CommandLineContainsLogin(commandLine, login)
                    ? new SystemGameProcess(process)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private string? GetProcessCommandLine(int processId)
        {
            var now = DateTime.UtcNow;
            if (_commandLineByPid.TryGetValue(processId, out var cached) && cached.ExpiresAt > now)
            {
                return cached.CommandLine;
            }

            var commandLine = TryGetProcessCommandLine(processId);
            _commandLineByPid[processId] = new CommandLineCacheEntry(commandLine, now + CommandLineCacheTtl);

            if (_commandLineByPid.Count > 512)
            {
                PruneExpiredCommandLines(now);
            }

            return commandLine;
        }

        private void PruneExpiredCommandLines(DateTime now)
        {
            foreach (var pid in _commandLineByPid.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToList())
            {
                _commandLineByPid.Remove(pid);
            }
        }

        public IGameProcess Launch(string gamePath, string login, string password)
        {
            var workingDir = Path.GetDirectoryName(gamePath);
            if (string.IsNullOrWhiteSpace(workingDir))
            {
                throw new InvalidOperationException("Cannot resolve working directory for game executable.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = gamePath,
                Arguments = BuildArguments(login, password),
                WorkingDirectory = workingDir,
                UseShellExecute = true
            };

            var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start game process.");
            return new SystemGameProcess(process);
        }

        public IntPtr WaitForMainWindowHandle(IGameProcess process, TimeSpan timeout)
        {
            if (process is not SystemGameProcess systemProcess)
            {
                throw new ArgumentException("Unsupported game process type.", nameof(process));
            }

            var startedAt = DateTime.UtcNow;

            while (!systemProcess.Process.HasExited && DateTime.UtcNow - startedAt < timeout)
            {
                systemProcess.Process.Refresh();

                if (systemProcess.Process.MainWindowHandle != IntPtr.Zero)
                {
                    return systemProcess.Process.MainWindowHandle;
                }

                Thread.Sleep(250);
            }

            return IntPtr.Zero;
        }

        public bool IsProcessAlive(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static bool CommandLineContainsLogin(string commandLine, string login)
        {
            var target = $"user:{login}";
            var index = commandLine.IndexOf(target, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var endIndex = index + target.Length;
                var hasBoundary = endIndex >= commandLine.Length ||
                                  char.IsWhiteSpace(commandLine[endIndex]) ||
                                  commandLine[endIndex] == '"';
                if (hasBoundary)
                {
                    return true;
                }

                index = commandLine.IndexOf(target, index + 1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static string BuildArguments(string login, string password)
        {
            var safeLogin = login.Replace("\"", string.Empty);
            var safePassword = password.Replace("\"", string.Empty);
            return $"startbypatcher user:{safeLogin} pwd:{safePassword}";
        }

        private static string? TryGetProcessCommandLine(int processId)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");

                var result = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                return result?["CommandLine"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private readonly struct CommandLineCacheEntry
        {
            public CommandLineCacheEntry(string? commandLine, DateTime expiresAt)
            {
                CommandLine = commandLine;
                ExpiresAt = expiresAt;
            }

            public string? CommandLine { get; }
            public DateTime ExpiresAt { get; }
        }
    }

    internal sealed class SystemGameProcess : IGameProcess
    {
        public SystemGameProcess(Process process)
        {
            Process = process;
        }

        public Process Process { get; }
        public int Id => Process.Id;
        public bool HasExited => Process.HasExited;
        public IntPtr MainWindowHandle => Process.MainWindowHandle;
    }
}
