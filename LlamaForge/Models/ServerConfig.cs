using System.Collections.Generic;

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

        // Inference Parameters
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 2048;
        public int MaxChatHistoryMessages { get; set; } = 20;

        // Logging
        public bool VerboseLogging { get; set; } = false;

        // Advanced
        public string AdditionalArgs { get; set; } = string.Empty;

        // WebUI Path (for serving static files)
        public string WebUIPath { get; set; } = string.Empty;

        public IEnumerable<string> Validate()
        {
            if (Port < 1 || Port > 65535)
                yield return "Port must be between 1 and 65535";
            if (ContextSize < 128)
                yield return "Context size must be at least 128";
            if (Threads < 1 || Threads > 256)
                yield return "Thread count must be between 1 and 256";
            if (BatchSize < 1)
                yield return "Batch size must be at least 1";
            if (BatchThreads < 1 || BatchThreads > 256)
                yield return "Batch thread count must be between 1 and 256";
            if (ParallelSlots < 1)
                yield return "Parallel slots must be at least 1";
            if (GpuLayers < 0)
                yield return "GPU layers cannot be negative";
            if (Timeout < 1)
                yield return "Timeout must be at least 1 second";
            if (Temperature < 0.0 || Temperature > 2.0)
                yield return "Temperature must be between 0.0 and 2.0";
            if (MaxTokens < 1)
                yield return "Max tokens must be at least 1";
            if (MaxChatHistoryMessages < 1)
                yield return "Max chat history messages must be at least 1";
        }
    }
}
