using System.Runtime.InteropServices;
using WindowsDesktop;

namespace PWInstanceLauncher.Services
{
    public class DesktopService
    {
        private readonly IDesktopAssignmentService _desktopAssignments;
        private readonly LogService _logService;

        public DesktopService()
            : this(new DesktopAssignmentService(), new LogService())
        {
        }

        public DesktopService(IDesktopAssignmentService desktopAssignments, LogService logService)
        {
            _desktopAssignments = desktopAssignments ?? throw new ArgumentNullException(nameof(desktopAssignments));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public void PlaceWindowOnCharacterDesktop(string login, IntPtr windowHandle)
        {
            if (!IsHandleValid(windowHandle, "PlaceWindowOnCharacterDesktop"))
            {
                return;
            }

            var desktop = GetOrCreateDesktop(login);
            if (desktop is null)
            {
                _logService.Warn($"Desktop for '{login}' is unavailable. Falling back to current desktop activation.");
                MoveWindowToCurrentDesktop(windowHandle);
                return;
            }

            if (!TryMoveToDesktop(windowHandle, desktop, $"Move window for '{login}' to assigned desktop"))
            {
                MoveWindowToCurrentDesktop(windowHandle);
                return;
            }

            if (!TrySwitchDesktop(desktop, $"Switch to assigned desktop for '{login}'"))
            {
                MoveWindowToCurrentDesktop(windowHandle);
                return;
            }

            ActivateWindow(windowHandle);
        }

        public void MoveWindowToCurrentDesktop(IntPtr windowHandle)
        {
            if (!IsHandleValid(windowHandle, "MoveWindowToCurrentDesktop"))
            {
                return;
            }

            VirtualDesktop? current = null;
            try
            {
                current = VirtualDesktop.Current;
            }
            catch (Exception ex)
            {
                _logService.Error("Failed to get current virtual desktop.", ex);
            }

            if (current is not null)
            {
                TryMoveToDesktop(windowHandle, current, "Move window to current desktop");
            }

            ActivateWindow(windowHandle);
        }

        public bool SwitchToDesktopWithWindow(IntPtr windowHandle)
        {
            if (!IsHandleValid(windowHandle, "SwitchToDesktopWithWindow"))
            {
                return false;
            }

            var desktop = TryGetDesktopFromWindow(windowHandle, "Resolve desktop from window handle");
            if (desktop is null)
            {
                MoveWindowToCurrentDesktop(windowHandle);
                return false;
            }

            if (!TrySwitchDesktop(desktop, "Switch to desktop discovered from window"))
            {
                MoveWindowToCurrentDesktop(windowHandle);
                return false;
            }

            ActivateWindow(windowHandle);
            return true;
        }

        public bool TrySwitchToCharacterDesktop(string login, IntPtr windowHandle)
        {
            if (!IsHandleValid(windowHandle, "TrySwitchToCharacterDesktop"))
            {
                return false;
            }

            if (!_desktopAssignments.TryGet(login, out var desktop) || desktop is null)
            {
                _logService.Warn($"No assigned desktop found for '{login}'.");
                MoveWindowToCurrentDesktop(windowHandle);
                return false;
            }

            if (!TrySwitchDesktop(desktop, $"Switch to character desktop for '{login}'"))
            {
                MoveWindowToCurrentDesktop(windowHandle);
                return false;
            }

            ActivateWindow(windowHandle);
            return true;
        }

        private VirtualDesktop? GetOrCreateDesktop(string login)
        {
            if (_desktopAssignments.TryGet(login, out var existing) && existing is not null)
            {
                return existing;
            }

            try
            {
                var created = VirtualDesktop.Create();
                return _desktopAssignments.Assign(login, created);
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to create/assign desktop for '{login}'.", ex);
                return null;
            }
        }

        public bool TryRepairCharacterDesktop(string login, IntPtr windowHandle)
        {
            return _desktopAssignments.RepairFromWindowHandle(login, windowHandle, out _);
        }

        public bool ReassignCharacterDesktop(string oldLogin, string newLogin)
        {
            return _desktopAssignments.Reassign(oldLogin, newLogin);
        }

        public bool UnassignCharacterDesktop(string login)
        {
            return _desktopAssignments.Unassign(login);
        }

        public void UnassignAllCharacterDesktops()
        {
            _desktopAssignments.UnassignAll();
        }

        private bool IsHandleValid(IntPtr windowHandle, string operation)
        {
            if (windowHandle == IntPtr.Zero)
            {
                _logService.Warn($"{operation} skipped because window handle is not available.");
                return false;
            }

            return true;
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

        private bool TryMoveToDesktop(IntPtr windowHandle, VirtualDesktop desktop, string operation)
        {
            try
            {
                VirtualDesktop.MoveToDesktop(windowHandle, desktop);
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error($"{operation} failed.", ex);
                return false;
            }
        }

        private bool TrySwitchDesktop(VirtualDesktop desktop, string operation)
        {
            try
            {
                desktop.Switch();
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error($"{operation} failed.", ex);
                return false;
            }
        }

        private VirtualDesktop? TryGetDesktopFromWindow(IntPtr windowHandle, string operation)
        {
            try
            {
                return VirtualDesktop.FromHwnd(windowHandle);
            }
            catch (Exception ex)
            {
                _logService.Error($"{operation} failed.", ex);
                return null;
            }
        }
    }
}
