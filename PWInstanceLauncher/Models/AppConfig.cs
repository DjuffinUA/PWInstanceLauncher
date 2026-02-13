namespace PWInstanceLauncher.Models
{
    public enum LaunchMode
    {
        SeparateDesktop,
        CurrentDesktop
    }

    public class AppConfig
    {
        public string GamePath { get; set; } = string.Empty;
        public LaunchMode LaunchMode { get; set; } = LaunchMode.SeparateDesktop;
        public List<CharacterProfile> Characters { get; set; } = new();
    }
}
