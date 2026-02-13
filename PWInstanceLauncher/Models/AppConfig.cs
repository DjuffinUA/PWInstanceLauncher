using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PWInstanceLauncher.Models
{
    internal class AppConfig
    {
        public string GamePath { get; set; } = "";
        public List<CharacterProfile> Characters { get; set; } = new();
    }
}
