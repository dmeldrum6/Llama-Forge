namespace LlamaForge.Models
{
    public class AppSettings
    {
        public ServerConfig ServerConfig { get; set; } = new ServerConfig();
        public LlamaVariantType? SelectedVariantType { get; set; }
        public bool ShowStartupScreen { get; set; } = true;
    }
}
