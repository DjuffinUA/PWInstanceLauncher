using System.Diagnostics;
using System.IO;
using System.Management;

namespace PWInstanceLauncher.Services
{
    internal class ProcessService : IProcessService
    {
        private const string ProcessName = "elementclient";

        public IGameProcess? TryFindRunningByLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return null;
            }

            foreach (var process in Process.GetProcessesByName(ProcessName))
            {
                var commandLine = TryGetProcessCommandLine(process.Id);
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    continue;
                }

                if (CommandLineContainsLogin(commandLine, login))
                {
                    return new SystemGameProcess(process);
                }
            }

            return null;
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
