using System;

namespace PWInstanceLauncher.Services
{
    internal interface IGameProcess
    {
        int Id { get; }
        bool HasExited { get; }
        IntPtr MainWindowHandle { get; }
    }

    internal interface IProcessService
    {
        IGameProcess? TryFindRunningByLogin(string login);
        IGameProcess Launch(string gamePath, string login, string password);
        IntPtr WaitForMainWindowHandle(IGameProcess process, TimeSpan timeout);
        bool IsProcessAlive(int processId);
    }
}
