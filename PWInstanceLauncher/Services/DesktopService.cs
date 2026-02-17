using System.Runtime.InteropServices;
using WindowsDesktop;

namespace PWInstanceLauncher.Services
{
    public class DesktopService
    {
        private readonly IDesktopAssignmentService _desktopAssignments;

        public DesktopService()
            : this(new DesktopAssignmentService())
        {
        }

        public DesktopService(IDesktopAssignmentService desktopAssignments)
        {
            _desktopAssignments = desktopAssignments ?? throw new ArgumentNullException(nameof(desktopAssignments));
        }

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
                if (desktop is null)
                {
                    return false;
                }

                desktop.Switch();
                ActivateWindow(windowHandle);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TrySwitchToCharacterDesktop(string login, IntPtr windowHandle)
        {
            if (!_desktopAssignments.TryGet(login, out var desktop) || desktop is null)
            {
                return false;
            }

            desktop.Switch();
            ActivateWindow(windowHandle);
            return true;
        }

        private VirtualDesktop GetOrCreateDesktop(string login)
        {
            if (_desktopAssignments.TryGet(login, out var existing) && existing is not null)
            {
                return existing;
            }

            var created = VirtualDesktop.Create();
            return _desktopAssignments.Assign(login, created);
        }

        public bool TryRepairCharacterDesktop(string login, IntPtr windowHandle)
        {
            return _desktopAssignments.RepairFromWindowHandle(login, windowHandle, out _);
        }

        public bool UnassignCharacterDesktop(string login)
        {
            return _desktopAssignments.Unassign(login);
        }

        public void UnassignAllCharacterDesktops()
        {
            _desktopAssignments.UnassignAll();
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
