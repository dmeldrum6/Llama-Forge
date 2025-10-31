namespace LlamaForge.Models
{
    public class ModelInfo
    {
        // Basic Model Information
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string OwnedBy { get; set; } = string.Empty;

        // Model Metadata
        public ModelMetadata? Meta { get; set; }

        // General Information (from GGUF metadata)
        public string? Architecture { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Finetune { get; set; }
        public string? License { get; set; }
        public string? SizeLabel { get; set; }
    }

    public class ModelMetadata
    {
        public int VocabType { get; set; }
        public int VocabSize { get; set; }
        public int TrainingContextLength { get; set; }
        public int EmbeddingDimensions { get; set; }
        public long ParameterCount { get; set; }
        public long ModelSize { get; set; }
    }
}
