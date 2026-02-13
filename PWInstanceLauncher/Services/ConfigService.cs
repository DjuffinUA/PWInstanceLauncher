using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using PWInstanceLauncher.Models;
using System.IO;

namespace PWInstanceLauncher.Services
{
    internal class ConfigService
    {
        private readonly string _configPath = "config.json";

        public AppConfig Load()
        {
            if (!File.Exists(_configPath))
            {
                var config = new AppConfig();
                Save(config);
                return config;
            }

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }

        public void Save(AppConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_configPath, json);
        }
    }
}
