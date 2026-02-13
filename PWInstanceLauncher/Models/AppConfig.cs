using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PWInstanceLauncher.Models
{
    public enum LaunchMode
    {
        SeparateDesktop,
        CurrentDesktop
    }
    public class AppConfig
    {
        public string GamePath { get; set; } = "";
        public LaunchMode LaunchMode { get; set; } = LaunchMode.SeparateDesktop;
        public List<CharacterProfile> Characters { get; set; } = new();
    }
}
