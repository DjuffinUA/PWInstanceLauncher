using System.Runtime.InteropServices;
using WindowsDesktop;

namespace PWInstanceLauncher.Services
{
    public class DesktopService
    {
        private readonly Dictionary<string, VirtualDesktop> _desktopByLogin = new(StringComparer.OrdinalIgnoreCase);

        public void PlaceWindowOnCharacterDesktop(string login, IntPtr windowHandle)
        {
            EnsureHandle(windowHandle);

            var desktop = GetOrCreateDesktop(login);
            VirtualDesktop.MoveToDesktop(windowHandle, desktop);
            desktop.Switch();
            ActivateWindow(windowHandle);
        }

        public void MoveWindowToCurrentDesktop(IntPtr windowHandle)
        {
            EnsureHandle(windowHandle);

            var current = VirtualDesktop.Current;
            VirtualDesktop.MoveToDesktop(windowHandle, current);
            ActivateWindow(windowHandle);
        }

        public bool SwitchToDesktopWithWindow(IntPtr windowHandle)
        {
            EnsureHandle(windowHandle);

            try
            {
                var desktop = VirtualDesktop.FromHwnd(windowHandle);
                desktop?.Switch();
                ActivateWindow(windowHandle);
                return desktop is not null;
            }
            catch
            {
                return false;
            }
        }

        public bool TrySwitchToCharacterDesktop(string login, IntPtr windowHandle)
        {
            if (!_desktopByLogin.TryGetValue(login, out var desktop))
            {
                return false;
            }

            desktop.Switch();
            ActivateWindow(windowHandle);
            return true;
        }

        private VirtualDesktop GetOrCreateDesktop(string login)
        {
            if (_desktopByLogin.TryGetValue(login, out var existing))
            {
                return existing;
            }

            var created = VirtualDesktop.Create();
            _desktopByLogin[login] = created;
            return created;
        }

        private static void EnsureHandle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Window handle is not available.", nameof(windowHandle));
            }
        }

        public void ActivateWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(windowHandle, 9); // SW_RESTORE
            SetForegroundWindow(windowHandle);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
