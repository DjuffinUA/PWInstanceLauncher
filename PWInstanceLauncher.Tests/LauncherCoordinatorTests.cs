using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;
using Xunit;

namespace PWInstanceLauncher.Tests;

public class LauncherCoordinatorTests
{
    [Fact]
    public void LaunchOrFocus_FocusesExistingRunningProcess()
    {
        var processService = new FakeProcessService();
        var desktopService = new FakeDesktopService { SwitchToCharacterDesktopResult = true };
        var credentialService = new FakeCredentialService();
        var profile = new CharacterProfile { Name = "Main", Login = "main", EncryptedPassword = "enc", RuntimeStatus = "Offline" };
        processService.RunningByLogin["main"] = new FakeGameProcess(100, hasExited: false, mainWindowHandle: new IntPtr(77));

        var sut = new LauncherCoordinator(processService, desktopService, credentialService);

        var result = sut.LaunchOrFocus(profile, "game.exe", LaunchMode.SeparateDesktop);

        Assert.Equal(LaunchActionType.FocusedExisting, result.ActionType);
        Assert.Equal("Running", profile.RuntimeStatus);
        Assert.Equal(0, processService.LaunchCalls);
        Assert.Equal(1, desktopService.TrySwitchToCharacterDesktopCalls);
    }

    [Fact]
    public void LaunchOrFocus_LaunchesAndPlacesWindowOnSeparateDesktop()
    {
        var processService = new FakeProcessService
        {
            ProcessToLaunch = new FakeGameProcess(200, hasExited: false, mainWindowHandle: IntPtr.Zero),
            WaitForWindowHandleResult = new IntPtr(123)
        };
        var desktopService = new FakeDesktopService();
        var credentialService = new FakeCredentialService { Decrypted = "plain" };
        var profile = new CharacterProfile { Name = "Main", Login = "main", EncryptedPassword = "enc", RuntimeStatus = "Offline" };

        var sut = new LauncherCoordinator(processService, desktopService, credentialService);

        var result = sut.LaunchOrFocus(profile, "game.exe", LaunchMode.SeparateDesktop);

        Assert.Equal(LaunchActionType.LaunchedNew, result.ActionType);
        Assert.Equal(200, result.ProcessId);
        Assert.Equal("enc", credentialService.DecryptInput);
        Assert.Equal(1, processService.LaunchCalls);
        Assert.Equal(1, desktopService.PlaceWindowCalls);
        Assert.Equal("Running", profile.RuntimeStatus);
    }

    [Fact]
    public void MonitorRunningProcesses_MarksTrackedCharacterOffline_WhenProcessDies()
    {
        var processService = new FakeProcessService();
        var desktopService = new FakeDesktopService();
        var credentialService = new FakeCredentialService();
        var profile = new CharacterProfile { Name = "Main", Login = "main", EncryptedPassword = "enc", RuntimeStatus = "Offline" };
        processService.RunningByLogin["main"] = new FakeGameProcess(300, hasExited: false, mainWindowHandle: new IntPtr(10));

        var sut = new LauncherCoordinator(processService, desktopService, credentialService);
        sut.InitializeRuntimeState(new[] { profile });

        processService.AliveByProcessId[300] = false;
        processService.RunningByLogin["main"] = null;

        var updates = sut.MonitorRunningProcesses(new[] { profile });

        Assert.Equal("Offline", profile.RuntimeStatus);
        Assert.Single(updates);
        Assert.Equal(1, desktopService.UnassignCalls);
    }


    [Fact]
    public void MonitorRunningProcesses_UsesDifferentiatedPolling_ForOfflineProfiles()
    {
        var processService = new FakeProcessService();
        var desktopService = new FakeDesktopService();
        var credentialService = new FakeCredentialService();
        var profile = new CharacterProfile { Name = "Main", Login = "main", EncryptedPassword = "enc", RuntimeStatus = "Offline" };
        processService.RunningByLogin["main"] = null;

        var sut = new LauncherCoordinator(processService, desktopService, credentialService);

        sut.MonitorRunningProcesses(new[] { profile });
        sut.MonitorRunningProcesses(new[] { profile });
        sut.MonitorRunningProcesses(new[] { profile });

        Assert.Equal(1, processService.TryFindCallsByLogin["main"]);
    }

    [Fact]
    public void MonitorRunningProcesses_DetectsOfflineProfile_OnScheduledProbe()
    {
        var processService = new FakeProcessService();
        var desktopService = new FakeDesktopService();
        var credentialService = new FakeCredentialService();
        var profile = new CharacterProfile { Name = "Main", Login = "main", EncryptedPassword = "enc", RuntimeStatus = "Offline" };
        processService.RunningByLogin["main"] = new FakeGameProcess(301, hasExited: false, mainWindowHandle: new IntPtr(11));

        var sut = new LauncherCoordinator(processService, desktopService, credentialService);

        sut.MonitorRunningProcesses(new[] { profile });
        sut.MonitorRunningProcesses(new[] { profile });
        sut.MonitorRunningProcesses(new[] { profile });

        Assert.Equal("Running", profile.RuntimeStatus);
    }
    [Fact]
    public void HandleLoginChange_UnassignsDesktop_WhenReassignFails()
    {
        var processService = new FakeProcessService();
        var desktopService = new FakeDesktopService { ReassignResult = false };
        var credentialService = new FakeCredentialService();

        var sut = new LauncherCoordinator(processService, desktopService, credentialService);

        sut.HandleLoginChange("old", "new");

        Assert.Equal(1, desktopService.ReassignCalls);
        Assert.Equal(1, desktopService.UnassignCalls);
    }

    private sealed class FakeProcessService : IProcessService
    {
        public Dictionary<string, FakeGameProcess?> RunningByLogin { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, bool> AliveByProcessId { get; } = new();
        public Dictionary<string, int> TryFindCallsByLogin { get; } = new(StringComparer.OrdinalIgnoreCase);
        public FakeGameProcess? ProcessToLaunch { get; set; }
        public IntPtr WaitForWindowHandleResult { get; set; }
        public int LaunchCalls { get; private set; }

        public bool IsProcessAlive(int processId)
        {
            return AliveByProcessId.TryGetValue(processId, out var alive) ? alive : true;
        }

        public IGameProcess Launch(string gamePath, string login, string password)
        {
            LaunchCalls++;
            return ProcessToLaunch ?? new FakeGameProcess(999, false, IntPtr.Zero);
        }

        public IGameProcess? TryFindRunningByLogin(string login)
        {
            TryFindCallsByLogin[login] = TryFindCallsByLogin.TryGetValue(login, out var count) ? count + 1 : 1;
            return RunningByLogin.TryGetValue(login, out var process) ? process : null;
        }

        public IntPtr WaitForMainWindowHandle(IGameProcess process, TimeSpan timeout)
        {
            return WaitForWindowHandleResult;
        }
    }

    private sealed class FakeDesktopService : IDesktopService
    {
        public bool SwitchToCharacterDesktopResult { get; set; }
        public bool ReassignResult { get; set; } = true;
        public int TrySwitchToCharacterDesktopCalls { get; private set; }
        public int PlaceWindowCalls { get; private set; }
        public int UnassignCalls { get; private set; }
        public int ReassignCalls { get; private set; }

        public void ActivateWindow(IntPtr windowHandle)
        {
        }

        public void MoveWindowToCurrentDesktop(IntPtr windowHandle)
        {
        }

        public void PlaceWindowOnCharacterDesktop(string login, IntPtr windowHandle)
        {
            PlaceWindowCalls++;
        }

        public bool ReassignCharacterDesktop(string oldLogin, string newLogin)
        {
            ReassignCalls++;
            return ReassignResult;
        }

        public bool SwitchToDesktopWithWindow(IntPtr windowHandle)
        {
            return false;
        }

        public bool TryRepairCharacterDesktop(string login, IntPtr windowHandle)
        {
            return false;
        }

        public bool TrySwitchToCharacterDesktop(string login, IntPtr windowHandle)
        {
            TrySwitchToCharacterDesktopCalls++;
            return SwitchToCharacterDesktopResult;
        }

        public bool UnassignCharacterDesktop(string login)
        {
            UnassignCalls++;
            return true;
        }
    }

    private sealed class FakeCredentialService : ICredentialService
    {
        public string Decrypted { get; set; } = "pwd";
        public string? DecryptInput { get; private set; }

        public string Decrypt(string protectedData)
        {
            DecryptInput = protectedData;
            return Decrypted;
        }
    }

    private sealed class FakeGameProcess : IGameProcess
    {
        public FakeGameProcess(int id, bool hasExited, IntPtr mainWindowHandle)
        {
            Id = id;
            HasExited = hasExited;
            MainWindowHandle = mainWindowHandle;
        }

        public int Id { get; }
        public bool HasExited { get; set; }
        public IntPtr MainWindowHandle { get; set; }
    }
}
