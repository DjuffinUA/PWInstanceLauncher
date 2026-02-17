using PWInstanceLauncher.Models;

namespace PWInstanceLauncher.Services
{
    internal enum LaunchActionType
    {
        None,
        FocusedExisting,
        LaunchedNew,
        Warning
    }

    internal sealed class LaunchActionResult
    {
        public LaunchActionResult(LaunchActionType actionType, string message, int? processId = null)
        {
            ActionType = actionType;
            Message = message;
            ProcessId = processId;
        }

        public LaunchActionType ActionType { get; }
        public string Message { get; }
        public int? ProcessId { get; }
    }

    internal class LauncherCoordinator
    {
        private static class DesktopEventCodes
        {
            public const string ExistingFocusStart = "DSK-001";
            public const string ExistingHandleMissing = "DSK-002";
            public const string SeparateDesktopSwitchSuccess = "DSK-003";
            public const string SeparateDesktopRepairSuccess = "DSK-004";
            public const string SeparateDesktopWindowDesktopFallback = "DSK-005";
            public const string SeparateDesktopActivateFallback = "DSK-006";
            public const string CurrentDesktopMove = "DSK-007";
            public const string LaunchHandleMissing = "DSK-008";
            public const string LaunchSeparateDesktop = "DSK-009";
            public const string LaunchCurrentDesktop = "DSK-010";
        }

        private readonly IProcessService _processService;
        private readonly IDesktopService _desktopService;
        private readonly ICredentialService _credentialService;
        private readonly LogService _logService;
        private readonly Dictionary<string, int> _runningProcessByLogin = new(StringComparer.OrdinalIgnoreCase);

        public LauncherCoordinator(IProcessService processService, IDesktopService desktopService, ICredentialService credentialService)
        {
            _processService = processService;
            _desktopService = desktopService;
            _credentialService = credentialService;
            _logService = new LogService();
        }

        public void InitializeRuntimeState(IEnumerable<CharacterProfile> profiles)
        {
            foreach (var profile in profiles)
            {
                SetStatus(profile, "Offline");

                if (string.IsNullOrWhiteSpace(profile.Login))
                {
                    continue;
                }

                var process = _processService.TryFindRunningByLogin(profile.Login);
                if (process is null || process.HasExited)
                {
                    continue;
                }

                RegisterRunningProcess(profile.Login, process.Id);
                SetStatus(profile, "Running");
            }
        }

        public IReadOnlyList<string> MonitorRunningProcesses(IReadOnlyCollection<CharacterProfile> profiles)
        {
            var updates = new List<string>();

            foreach (var login in _runningProcessByLogin.Keys.ToList())
            {
                if (_processService.IsProcessAlive(_runningProcessByLogin[login]))
                {
                    continue;
                }

                _runningProcessByLogin.Remove(login);
                if (ShouldUnassignDesktop(login))
                {
                    _desktopService.UnassignCharacterDesktop(login);
                }

                SetStatusByLogin(profiles, login, "Offline");
                updates.Add($"{login} switched to Offline.");
            }

            foreach (var profile in profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Login) || _runningProcessByLogin.ContainsKey(profile.Login))
                {
                    continue;
                }

                var process = _processService.TryFindRunningByLogin(profile.Login);
                if (process is null || process.HasExited)
                {
                    TransitionToOffline(profile);
                    continue;
                }

                RegisterRunningProcess(profile.Login, process.Id);
                SetStatus(profile, "Running");
            }

            return updates;
        }

        public LaunchActionResult LaunchOrFocus(CharacterProfile profile, string gamePath, LaunchMode mode)
        {
            var existingProcess = _processService.TryFindRunningByLogin(profile.Login);
            if (existingProcess is not null && !existingProcess.HasExited)
            {
                RegisterRunningProcess(profile.Login, existingProcess.Id);
                SetStatus(profile, "Running");

                LogDesktopDecision(
                    DesktopEventCodes.ExistingFocusStart,
                    profile.Login,
                    existingProcess.Id,
                    existingProcess.MainWindowHandle,
                    mode,
                    "switch",
                    "Running process detected; attempting to focus and align desktop.");

                var focusResult = FocusExistingCharacter(existingProcess, profile.Login, mode);
                return focusResult
                    ? new LaunchActionResult(LaunchActionType.FocusedExisting, $"Focused running character '{profile.Name}'.")
                    : new LaunchActionResult(LaunchActionType.Warning, "Running process found, but window handle is unavailable.");
            }

            var password = _credentialService.Decrypt(profile.EncryptedPassword);
            var process = _processService.Launch(gamePath, profile.Login, password);
            RegisterRunningProcess(profile.Login, process.Id);
            SetStatus(profile, "Running");

            var windowHandle = _processService.WaitForMainWindowHandle(process, TimeSpan.FromSeconds(30));
            if (windowHandle == IntPtr.Zero)
            {
                LogDesktopDecision(
                    DesktopEventCodes.LaunchHandleMissing,
                    profile.Login,
                    process.Id,
                    windowHandle,
                    mode,
                    "fallback",
                    "Process launched but main window handle was not detected within timeout.");

                return new LaunchActionResult(LaunchActionType.Warning, "Process started, but main window handle was not detected within timeout.", process.Id);
            }

            if (mode == LaunchMode.SeparateDesktop)
            {
                _desktopService.PlaceWindowOnCharacterDesktop(profile.Login, windowHandle);
                LogDesktopDecision(
                    DesktopEventCodes.LaunchSeparateDesktop,
                    profile.Login,
                    process.Id,
                    windowHandle,
                    mode,
                    "switch",
                    "New process launched and routed via PlaceWindowOnCharacterDesktop.");
            }
            else
            {
                _desktopService.MoveWindowToCurrentDesktop(windowHandle);
                LogDesktopDecision(
                    DesktopEventCodes.LaunchCurrentDesktop,
                    profile.Login,
                    process.Id,
                    windowHandle,
                    mode,
                    "move",
                    "New process launched and moved to current desktop.");
            }

            return new LaunchActionResult(LaunchActionType.LaunchedNew, $"Character '{profile.Name}' launched.", process.Id);
        }

        public void HandleLoginChange(string oldLogin, string newLogin)
        {
            if (string.IsNullOrWhiteSpace(oldLogin))
            {
                return;
            }

            _runningProcessByLogin.Remove(oldLogin);

            if (string.IsNullOrWhiteSpace(newLogin) ||
                !_desktopService.ReassignCharacterDesktop(oldLogin, newLogin))
            {
                _desktopService.UnassignCharacterDesktop(oldLogin);
            }
        }

        public void CleanupRuntimeMappings(string? login, bool forceDesktopUnassign = false)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return;
            }

            _runningProcessByLogin.Remove(login);
            if (forceDesktopUnassign || ShouldUnassignDesktop(login))
            {
                _desktopService.UnassignCharacterDesktop(login);
            }
        }

        public void PruneUnknownLogins(IEnumerable<string> knownLogins)
        {
            var known = new HashSet<string>(knownLogins.Where(login => !string.IsNullOrWhiteSpace(login)), StringComparer.OrdinalIgnoreCase);

            foreach (var login in _runningProcessByLogin.Keys.Where(login => !known.Contains(login)).ToList())
            {
                CleanupRuntimeMappings(login, forceDesktopUnassign: true);
            }
        }

        private bool FocusExistingCharacter(IGameProcess process, string login, LaunchMode mode)
        {
            var windowHandle = process.MainWindowHandle;
            if (windowHandle == IntPtr.Zero)
            {
                windowHandle = _processService.WaitForMainWindowHandle(process, TimeSpan.FromSeconds(5));
            }

            if (windowHandle == IntPtr.Zero)
            {
                LogDesktopDecision(
                    DesktopEventCodes.ExistingHandleMissing,
                    login,
                    process.Id,
                    windowHandle,
                    mode,
                    "fallback",
                    "Existing process found but window handle is unavailable.");
                return false;
            }

            if (mode == LaunchMode.SeparateDesktop)
            {
                var switched = _desktopService.TrySwitchToCharacterDesktop(login, windowHandle);
                if (switched)
                {
                    LogDesktopDecision(
                        DesktopEventCodes.SeparateDesktopSwitchSuccess,
                        login,
                        process.Id,
                        windowHandle,
                        mode,
                        "switch",
                        "Switched to assigned character desktop.");
                }

                if (!switched && _desktopService.TryRepairCharacterDesktop(login, windowHandle))
                {
                    switched = _desktopService.TrySwitchToCharacterDesktop(login, windowHandle);
                    if (switched)
                    {
                        LogDesktopDecision(
                            DesktopEventCodes.SeparateDesktopRepairSuccess,
                            login,
                            process.Id,
                            windowHandle,
                            mode,
                            "switch",
                            "Desktop mapping was repaired from window handle and switch succeeded.");
                    }
                }

                var switchedByWindowDesktop = !switched && _desktopService.SwitchToDesktopWithWindow(windowHandle);
                switched = switched || switchedByWindowDesktop;
                if (switchedByWindowDesktop)
                {
                    LogDesktopDecision(
                        DesktopEventCodes.SeparateDesktopWindowDesktopFallback,
                        login,
                        process.Id,
                        windowHandle,
                        mode,
                        "fallback",
                        "Switched by resolving desktop directly from window handle.");
                }

                if (!switched)
                {
                    _desktopService.ActivateWindow(windowHandle);
                    LogDesktopDecision(
                        DesktopEventCodes.SeparateDesktopActivateFallback,
                        login,
                        process.Id,
                        windowHandle,
                        mode,
                        "fallback",
                        "Desktop switch failed; window activation used as fallback.");
                }

                return true;
            }

            _desktopService.MoveWindowToCurrentDesktop(windowHandle);
            LogDesktopDecision(
                DesktopEventCodes.CurrentDesktopMove,
                login,
                process.Id,
                windowHandle,
                mode,
                "move",
                "Existing process moved to current desktop mode.");
            return true;
        }

        private void LogDesktopDecision(
            string eventCode,
            string login,
            int processId,
            IntPtr windowHandle,
            LaunchMode mode,
            string result,
            string reason)
        {
            _logService.Info($"{eventCode} login='{login}', pid={processId}, hwnd=0x{windowHandle.ToInt64():X}, mode={mode}, result={result} | {reason}");
        }

        private void RegisterRunningProcess(string login, int processId)
        {
            _runningProcessByLogin[login] = processId;
        }

        private void TransitionToOffline(CharacterProfile profile)
        {
            if (string.Equals(profile.RuntimeStatus, "Running", StringComparison.OrdinalIgnoreCase))
            {
                CleanupRuntimeMappings(profile.Login);
            }

            SetStatus(profile, "Offline");
        }

        private bool ShouldUnassignDesktop(string login)
        {
            var process = _processService.TryFindRunningByLogin(login);
            return process is null || process.HasExited;
        }

        private static void SetStatus(CharacterProfile profile, string status)
        {
            profile.RuntimeStatus = status;
        }

        private static void SetStatusByLogin(IEnumerable<CharacterProfile> profiles, string login, string status)
        {
            var profile = profiles.FirstOrDefault(c => string.Equals(c.Login, login, StringComparison.OrdinalIgnoreCase));
            if (profile is not null)
            {
                SetStatus(profile, status);
            }
        }
    }
}
