using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsDesktop;

namespace PWInstanceLauncher.Services
{
    public class DesktopService
    {
        public void MoveToNewDesktop(IntPtr windowHandle)
        {
            var newDesktop = VirtualDesktop.Create();
            VirtualDesktop.MoveToDesktop(windowHandle, newDesktop);
            newDesktop.Switch();
        }

        public void MoveToCurrentDesktop(IntPtr windowHandle)
        {
            var current = VirtualDesktop.Current;
            VirtualDesktop.MoveToDesktop(windowHandle, current);
        }
    }
}
