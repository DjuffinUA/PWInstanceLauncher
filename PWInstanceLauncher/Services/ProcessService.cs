using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;

namespace PWInstanceLauncher.Services
{
    internal class ProcessService
    {
        private const string ProcessName = "elementclient";

        public bool IsGameExecutableValid(string gamePath)
        {
            return !string.IsNullOrWhiteSpace(gamePath) &&
                   File.Exists(gamePath) &&
                   Path.GetExtension(gamePath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        public Process? TryFindRunningByLogin(string login)
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

                var loginToken = $"user:{login}";
                if (commandLine.Contains(loginToken, StringComparison.OrdinalIgnoreCase))
                {
                    return process;
                }
            }

            return null;
        }

        public Process Launch(string gamePath, string login, string password)
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

            return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start game process.");
        }

        public IntPtr WaitForMainWindowHandle(Process process, TimeSpan timeout)
        {
            var startedAt = DateTime.UtcNow;

            while (!process.HasExited && DateTime.UtcNow - startedAt < timeout)
            {
                process.Refresh();

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }

                Thread.Sleep(250);
            }

            return IntPtr.Zero;
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
}
