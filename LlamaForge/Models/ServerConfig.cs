namespace LlamaForge.Models
{
    public class ServerConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        public string ModelPath { get; set; } = string.Empty;
        public int ContextSize { get; set; } = 2048;
        public int Threads { get; set; } = 4;
        public int GpuLayers { get; set; } = 0;
        public string AdditionalArgs { get; set; } = string.Empty;
    }
}
