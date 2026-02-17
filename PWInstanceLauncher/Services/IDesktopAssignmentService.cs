using WindowsDesktop;

namespace PWInstanceLauncher.Services
{
    public interface IDesktopAssignmentService
    {
        VirtualDesktop Assign(string login, VirtualDesktop desktop);
        bool TryGet(string login, out VirtualDesktop? desktop);
        bool Unassign(string login);
        void UnassignAll();
        bool RepairFromWindowHandle(string login, IntPtr windowHandle, out VirtualDesktop? desktop);
    }
}
