# Llama Forge

A comprehensive WPF (Windows Presentation Foundation) wrapper for [llama.cpp](https://github.com/ggerganov/llama.cpp), providing an intuitive graphical interface for managing and interacting with local large language models.

## Features

- **Multi-Variant Support**: Download and manage multiple llama.cpp variants:
  - CPU (AVX2)
  - CUDA (NVIDIA GPU)
  - ROCm (AMD GPU)
  - Vulkan
  - SYCL (Intel GPU)

- **Server Management**:
  - Start/stop local llama.cpp server instances
  - Real-time server logs and monitoring
  - Configurable server parameters (threads, context size, GPU layers, etc.)

- **Chat Interface**:
  - Clean, modern chat UI for interacting with models
  - Streaming responses
  - Conversation history

- **Automatic Updates**:
  - Check for latest llama.cpp releases from GitHub
  - One-click download and installation
  - Version tracking for installed variants

## Prerequisites

- Windows 10/11
- .NET 8.0 Runtime or SDK
- A GGUF format model file (can be downloaded from [Hugging Face](https://huggingface.co/models?search=gguf))

## Building from Source

1. Clone the repository:
```bash
git clone https://github.com/yourusername/Llama-Forge.git
cd Llama-Forge
```

2. Build the project:
```bash
cd LlamaForge
dotnet build
```

3. Run the application:
```bash
dotnet run
```

## Quick Start Guide

### 1. Download llama.cpp

1. Launch Llama Forge
2. Navigate to the **Download / Update** tab
3. Select your preferred variant:
   - Choose **CUDA** if you have an NVIDIA GPU
   - Choose **CPU** for CPU-only execution
   - Choose **ROCm** for AMD GPUs
   - Choose **Vulkan** for cross-platform GPU support
4. Click **Check for Updates** to see the latest version
5. Click **Download Selected** to download and install

### 2. Get a Model

Download a GGUF model file. Some popular options:

- [Llama 3.2 models](https://huggingface.co/models?search=llama-3.2-gguf)
- [Mistral models](https://huggingface.co/models?search=mistral-gguf)
- [Phi models](https://huggingface.co/models?search=phi-gguf)

Recommended for testing: Small models like `Phi-3-mini-4k-instruct-q4.gguf` or `Llama-3.2-1B-Instruct-Q4_K_M.gguf`

### 3. Configure and Start Server

1. Navigate to the **Server** tab
2. Click **Browse...** and select your GGUF model file
3. Configure server settings:
   - **Host**: Keep as `127.0.0.1` for local access
   - **Port**: Default `8080` (change if needed)
   - **Context Size**: `2048` is a good starting point
   - **Threads**: Set to your CPU core count
   - **GPU Layers**:
     - Set to `0` for CPU-only
     - Set to `32` or higher to offload layers to GPU (requires CUDA/ROCm/Vulkan variant)
4. Click **Start Server**
5. Wait for the server to start (watch the logs)

### 4. Start Chatting

1. Navigate to the **Chat** tab
2. Type your message in the text box
3. Press **Ctrl+Enter** or click **Send**
4. Watch the response stream in real-time!

## Configuration

### Server Parameters

- **Model Path**: Path to your GGUF model file
- **Host**: Server host address (default: 127.0.0.1)
- **Port**: Server port (default: 8080)
- **Context Size**: Maximum context length in tokens (larger = more memory)
- **Threads**: Number of CPU threads to use
- **GPU Layers**: Number of model layers to offload to GPU (0 = CPU only)
- **Additional Args**: Any additional llama.cpp command-line arguments

### GPU Acceleration

To use GPU acceleration:

1. Download the appropriate variant (CUDA for NVIDIA, ROCm for AMD, etc.)
2. Set **GPU Layers** to a value greater than 0
   - Start with 32 and adjust based on your GPU memory
   - More layers = faster inference but requires more VRAM

## Project Structure

```
LlamaForge/
├── Models/              # Data models
│   ├── ChatMessage.cs
│   ├── ServerConfig.cs
│   ├── LlamaVariant.cs
│   └── GitHubRelease.cs
├── Services/            # Core services
│   ├── GitHubService.cs        # GitHub API integration
│   ├── LlamaServerManager.cs   # Server process management
│   └── LlamaChatClient.cs      # Chat API client
├── ViewModels/          # MVVM view models
│   └── MainViewModel.cs
├── Views/               # UI views (currently empty, using MainWindow)
├── Helpers/             # Utility classes
│   └── InverseBooleanConverter.cs
├── MainWindow.xaml      # Main application window
├── App.xaml            # Application entry point
└── LlamaForge.csproj   # Project file
```

## Architecture

Llama Forge follows the MVVM (Model-View-ViewModel) pattern:

- **Models**: Define data structures for chat messages, server configuration, etc.
- **Services**: Handle business logic (GitHub API, server management, chat client)
- **ViewModels**: Bridge between views and services, handle UI state and commands
- **Views**: XAML-based UI components

## Troubleshooting

### Server won't start

1. Check that the model file path is correct
2. Verify the selected variant is downloaded (check Download tab)
3. Look at Server Logs for error messages
4. Ensure the port is not already in use

### GPU not being used

1. Verify you downloaded the correct variant (CUDA/ROCm/Vulkan)
2. Check that **GPU Layers** is set to a value > 0
3. Ensure you have the appropriate GPU drivers installed
4. Check server logs for GPU detection messages

### Download fails

1. Check your internet connection
2. Verify GitHub is accessible
3. Try a different variant
4. Check the server logs for detailed error messages

### Chat responses are slow

1. Increase **GPU Layers** if you have a GPU
2. Try a smaller/quantized model
3. Reduce **Context Size**
4. Increase **Threads** (but not beyond your CPU core count)

## Technical Details

### Dependencies

- **Newtonsoft.Json**: JSON serialization/deserialization
- **CommunityToolkit.Mvvm**: MVVM helpers and commands

### llama.cpp Integration

Llama Forge integrates with llama.cpp through:

1. **Process Management**: Spawns llama-server.exe as a child process
2. **HTTP API**: Communicates via llama.cpp's HTTP API endpoints:
   - `/v1/chat/completions`: OpenAI-compatible chat endpoint
   - `/completion`: Raw completion endpoint
   - `/health`: Server health check

### Storage

- Downloaded llama.cpp variants: `%LocalAppData%\LlamaForge\llama-cpp\`
- Each variant is stored in its own subdirectory
- Version tracking via `version.txt` file in each variant directory

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## Roadmap

Phase 1 (Current):
- [x] Basic WPF UI
- [x] Server management
- [x] Chat interface
- [x] Multi-variant support
- [x] Automatic updates

Phase 2 (Future):
- [ ] Model download manager
- [ ] Preset configurations
- [ ] Multiple server instances
- [ ] Advanced chat features (system prompts, temperature control)
- [ ] Model quantization tools
- [ ] Settings persistence
- [ ] Themes support

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [llama.cpp](https://github.com/ggerganov/llama.cpp) - The amazing C++ implementation of LLaMA
- [Georgi Gerganov](https://github.com/ggerganov) - Creator of llama.cpp
- The open-source AI community

## Support

For issues, questions, or suggestions:
- Open an issue on GitHub
- Check existing issues for solutions
- Review the troubleshooting section above

---

**Note**: This is a wrapper application. All AI inference is performed by llama.cpp. Model quality and performance depend on the underlying llama.cpp implementation and the models you use.
