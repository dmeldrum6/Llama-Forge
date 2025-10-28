using System.Collections.Generic;

namespace LlamaForge.Models
{
    public enum LlamaVariantType
    {
        CPU,
        CUDA,
        ROCm,
        Vulkan,
        SYCL,
        OpenCL
    }

    public class LlamaVariant
    {
        public LlamaVariantType Type { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string AssetNamePattern { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public static List<LlamaVariant> GetAvailableVariants()
        {
            return new List<LlamaVariant>
            {
                new LlamaVariant
                {
                    Type = LlamaVariantType.CPU,
                    DisplayName = "CPU",
                    AssetNamePattern = "llama-*-bin-win-cpu-x64.zip",
                    Description = "CPU-only version (AVX/AVX2/AVX-512 support)"
                },
                new LlamaVariant
                {
                    Type = LlamaVariantType.CUDA,
                    DisplayName = "CUDA (NVIDIA GPU)",
                    AssetNamePattern = "llama-*-bin-win-cuda-*-x64.zip",
                    Description = "NVIDIA GPU acceleration with CUDA"
                },
                new LlamaVariant
                {
                    Type = LlamaVariantType.Vulkan,
                    DisplayName = "Vulkan",
                    AssetNamePattern = "llama-*-bin-win-vulkan-x64.zip",
                    Description = "Vulkan GPU acceleration (cross-platform)"
                },
                new LlamaVariant
                {
                    Type = LlamaVariantType.ROCm,
                    DisplayName = "HIP/ROCm (AMD GPU)",
                    AssetNamePattern = "llama-*-bin-win-hip-*-x64.zip",
                    Description = "AMD GPU acceleration with HIP/ROCm"
                },
                new LlamaVariant
                {
                    Type = LlamaVariantType.SYCL,
                    DisplayName = "SYCL (Intel GPU)",
                    AssetNamePattern = "llama-*-bin-win-sycl-x64.zip",
                    Description = "Intel GPU acceleration with SYCL"
                }
            };
        }
    }
}
