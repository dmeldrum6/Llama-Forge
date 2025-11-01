namespace LlamaForge.Models
{
    public class ServerConfig
    {
        // Basic Configuration
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        public string ModelPath { get; set; } = string.Empty;
        public int ContextSize { get; set; } = 2048;

        // Performance Settings
        public int Threads { get; set; } = 4;
        public int BatchSize { get; set; } = 512;
        public int BatchThreads { get; set; } = 4;
        public int ParallelSlots { get; set; } = 1;
        public bool ContinuousBatching { get; set; } = false;
        public int GpuLayers { get; set; } = 0;

        // Memory Management
        public bool MemoryLock { get; set; } = false;
        public bool DisableMemoryMapping { get; set; } = false;

        // Server Features
        public string ModelAlias { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int Timeout { get; set; } = 600;
        public bool EnableEmbeddings { get; set; } = false;
        public string SystemPrompt { get; set; } = "You are a helpful assistant";

        // Advanced
        public string AdditionalArgs { get; set; } = string.Empty;
    }
}
