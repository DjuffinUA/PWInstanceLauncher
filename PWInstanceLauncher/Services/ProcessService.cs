using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace PWInstanceLauncher.Services
{
    internal class ProcessService
    {
        public void Launch(string gamePath, string login, string password)
        {
            var workingDir = Path.GetDirectoryName(gamePath);

            var psi = new ProcessStartInfo
            {
                FileName = gamePath,
                Arguments = $"startbypatcher user:{login} pwd:{password}",
                WorkingDirectory = workingDir,
                UseShellExecute = true
            };

            Process.Start(psi);
        }
    }
}
