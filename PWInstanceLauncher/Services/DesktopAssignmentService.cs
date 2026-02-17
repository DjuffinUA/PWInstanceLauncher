using WindowsDesktop;

namespace PWInstanceLauncher.Services
{
    public class DesktopAssignmentService : IDesktopAssignmentService
    {
        private readonly Dictionary<string, VirtualDesktop> _desktopByLogin = new(StringComparer.OrdinalIgnoreCase);
        private readonly LogService _logService = new();

        public VirtualDesktop Assign(string login, VirtualDesktop desktop)
        {
            EnsureLogin(login);
            ArgumentNullException.ThrowIfNull(desktop);

            _desktopByLogin[login] = desktop;
            return desktop;
        }

        public bool TryGet(string login, out VirtualDesktop? desktop)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                desktop = null;
                return false;
            }

            return _desktopByLogin.TryGetValue(login, out desktop);
        }

        public bool Reassign(string oldLogin, string newLogin)
        {
            EnsureLogin(oldLogin);
            EnsureLogin(newLogin);

            if (string.Equals(oldLogin, newLogin, StringComparison.OrdinalIgnoreCase))
            {
                return _desktopByLogin.ContainsKey(oldLogin);
            }

            if (!_desktopByLogin.TryGetValue(oldLogin, out var desktop) || desktop is null)
            {
                return false;
            }

            _desktopByLogin.Remove(oldLogin);
            _desktopByLogin[newLogin] = desktop;
            return true;
        }

        public bool Unassign(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return false;
            }

            return _desktopByLogin.Remove(login);
        }

        public void UnassignAll()
        {
            _desktopByLogin.Clear();
        }

        public bool RepairFromWindowHandle(string login, IntPtr windowHandle, out VirtualDesktop? desktop)
        {
            EnsureLogin(login);

            if (windowHandle == IntPtr.Zero)
            {
                desktop = null;
                return false;
            }

            try
            {
                var detectedDesktop = VirtualDesktop.FromHwnd(windowHandle);
                if (detectedDesktop is null)
                {
                    desktop = null;
                    return false;
                }

                desktop = Assign(login, detectedDesktop);
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to repair desktop assignment for '{login}' from window handle.", ex);
                desktop = null;
                return false;
            }
        }

        private static void EnsureLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                throw new ArgumentException("Login is required for desktop mapping.", nameof(login));
            }
        }
    }
}
