using System;

namespace PWInstanceLauncher.Services
{
    internal interface IDesktopService
    {
        void PlaceWindowOnCharacterDesktop(string login, IntPtr windowHandle);
        void MoveWindowToCurrentDesktop(IntPtr windowHandle);
        bool SwitchToDesktopWithWindow(IntPtr windowHandle);
        bool TrySwitchToCharacterDesktop(string login, IntPtr windowHandle);
        bool TryRepairCharacterDesktop(string login, IntPtr windowHandle);
        bool ReassignCharacterDesktop(string oldLogin, string newLogin);
        bool UnassignCharacterDesktop(string login);
        void ActivateWindow(IntPtr windowHandle);
    }
}
