# Llama Forge

A comprehensive WPF (Windows Presentation Foundation) wrapper for [llama.cpp](https://github.com/ggerganov/llama.cpp), providing an intuitive graphical interface for managing and interacting with local large language models.

<img width="1184" height="793" alt="image" src="https://github.com/user-attachments/assets/839f03c0-1edf-4d92-af56-78a76734232f" />

## Features

- **Multi-Variant Support**: Download and manage multiple llama.cpp variants:
  - CPU (AVX/AVX2/AVX-512)
  - CUDA (NVIDIA GPU)
  - Vulkan (cross-platform GPU)
  - HIP/ROCm (AMD GPU)
  - SYCL (Intel GPU)

- **Server Management**:
  - Start/stop local llama.cpp server instances
  - Real-time server logs and monitoring
  - Loading indicator while the model initializes
  - Comprehensive configurable server parameters (see [Server Parameters](#server-parameters))

- **Web-Based Chat**:
  - Automatically downloads llama.cpp's built-in WebUI
  - **Launch Chat Client** button opens the chat interface in your default browser once the server is ready
  - Full-featured web chat with streaming responses, conversation history, and model settings

- **Theme Support**:
  - Toggle between dark and light themes at any time

- **Settings Persistence**:
  - All server configuration and preferences are saved automatically
  - Option to suppress the startup welcome screen

- **Automatic Updates**:
  - Check for the latest llama.cpp releases from GitHub
  - One-click download and installation
  - Download progress display with cancel support
  - Version tracking for installed variants

## Prerequisites

- Windows 10/11
- .NET 8.0 Runtime or SDK
- A GGUF format model file (can be downloaded from [Hugging Face](https://huggingface.co/models?search=gguf))

## Building from Source

1. Clone the repository:
```bash
git clone https://github.com/dmeldrum6/Llama-Forge.git
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

On first launch, a welcome screen walks you through the five setup steps. You can disable it via the "Don't show this screen again" checkbox.

### 1. Download llama.cpp

1. Launch Llama Forge
2. Navigate to the **Download / Update** tab
3. Select your preferred variant:
   - **CUDA** — NVIDIA GPU (recommended if you have an NVIDIA card)
   - **Vulkan** — Cross-platform GPU acceleration
   - **HIP/ROCm** — AMD GPU
   - **SYCL** — Intel GPU
   - **CPU** — CPU-only execution
4. Click **Check for Updates** to see the latest available version
5. Click **Download Selected** to download and install

### 2. Get a Model

Download a GGUF model file. Some popular options:

- [Llama 3 models](https://huggingface.co/models?search=llama-3-gguf)
- [Mistral models](https://huggingface.co/models?search=mistral-gguf)
- [Phi models](https://huggingface.co/models?search=phi-gguf)

Recommended for testing: small models like `Phi-3-mini-4k-instruct-q4.gguf` or `Llama-3.2-1B-Instruct-Q4_K_M.gguf`.

### 3. Configure and Start the Server

1. Navigate to the **Server** tab
2. Select the llama.cpp variant you downloaded
3. Click **Browse...** and select your GGUF model file
4. Adjust settings as needed (see [Server Parameters](#server-parameters) below)
5. Click **Start Server**
6. A loading indicator will appear while the model initializes — wait for it to complete

### 4. Start Chatting

Once the model finishes loading, the **Launch Chat Client** button in the Server tab becomes active. Click it to open llama.cpp's built-in web chat interface in your default browser.

## Configuration

### Server Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| Model Path | _(empty)_ | Path to your GGUF model file |
| Host | `127.0.0.1` | Server listen address |
| Port | `8080` | Server port (1–65535) |
| Context Size | `2048` | Maximum context length in tokens |
| Threads | `4` | CPU threads for prompt processing |
| Batch Size | `512` | Prompt processing batch size |
| Batch Threads | `4` | Threads used for batching |
| Parallel Slots | `1` | Number of parallel request slots |
| Continuous Batching | `false` | Enable continuous batching |
| GPU Layers | `0` | Model layers to offload to GPU (0 = CPU only) |
| Memory Lock | `false` | Lock model in RAM to prevent swapping |
| Disable Memory Mapping | `false` | Disable mmap for model loading |
| Model Alias | _(empty)_ | Alias name reported by the API |
| API Key | _(empty)_ | Optional API key for server access |
| Timeout | `600` | Request timeout in seconds |
| Enable Embeddings | `false` | Expose the embeddings endpoint |
| System Prompt | `You are a helpful assistant` | Default system prompt for chat |
| Temperature | `0.7` | Sampling temperature (0.0–2.0) |
| Max Tokens | `2048` | Maximum tokens per response |
| Max Chat History | `20` | Number of past messages sent as context |
| Verbose Logging | `false` | Enable detailed server log output |
| Additional Args | _(empty)_ | Extra llama.cpp command-line arguments |

### GPU Acceleration

1. Download the variant that matches your GPU (CUDA → NVIDIA, HIP/ROCm → AMD, Vulkan → any modern GPU, SYCL → Intel)
2. Set **GPU Layers** to a value greater than `0`
   - Start with `32` and increase until you run out of VRAM
   - More layers = faster inference but higher VRAM usage
3. Ensure the appropriate drivers are installed for your GPU

### Auto-Detect Threads

Click **Auto-Detect** next to the Threads field to automatically set the value to your logical CPU core count.

## Project Structure

```
LlamaForge/
├── Controls/            # Custom UI controls
│   └── MessageContentControl.xaml  # Content rendering control (text and syntax-highlighted code blocks)
├── Helpers/             # Utility classes
│   ├── InverseBooleanConverter.cs
│   └── InverseBooleanToVisibilityConverter.cs
├── Models/              # Data models
│   ├── AppSettings.cs          # Persisted application settings
│   ├── ChatMessage.cs          # Chat message representation
│   ├── DownloadableVariant.cs  # Downloadable release asset info
│   ├── GitHubRelease.cs        # GitHub API release model
│   ├── LlamaVariant.cs         # Variant type definitions
│   ├── ModelInfo.cs            # Model metadata from llama.cpp API
│   └── ServerConfig.cs         # Full server configuration
├── Services/            # Core services
│   ├── GitHubService.cs        # GitHub API integration & binary management
│   ├── LlamaChatClient.cs      # Chat API client (streaming)
│   ├── LlamaServerManager.cs   # Server process lifecycle management
│   └── SettingsService.cs      # Load/save settings from disk
├── ViewModels/          # MVVM view models
│   └── MainViewModel.cs        # Central application state and commands
├── App.xaml            # Application entry point & startup screen logic
├── MainWindow.xaml     # Main application window
├── StartupScreen.xaml  # First-run welcome/onboarding screen
└── LlamaForge.csproj   # Project file
```

## Architecture

Llama Forge follows the MVVM (Model-View-ViewModel) pattern:

- **Models**: Data structures for chat messages, server configuration, settings, and model metadata
- **Services**: Business logic — GitHub API calls, server process management, chat streaming, settings persistence
- **ViewModels**: Bridges views and services; manages all UI state and commands
- **Controls**: Custom WPF controls (e.g., `MessageContentControl` for rendering text with syntax-highlighted code blocks)

## Storage

- Settings: `%LocalAppData%\LlamaForge\settings.json`
- Downloaded llama.cpp variants: `%LocalAppData%\LlamaForge\llama-cpp\<variant>\`
- WebUI files: stored alongside the llama.cpp variant binaries

## Troubleshooting

### Server won't start

1. Verify the correct variant is downloaded (check the **Download / Update** tab)
2. Confirm the model file path is valid
3. Check **Server Logs** for specific error messages
4. Ensure the configured port is not already in use

### Launch Chat Client button is greyed out

- A loading indicator appears after the server process starts; wait for it to complete before the button becomes active
- If it takes unusually long, check the **Server Logs** tab for errors during model loading

### GPU not being used

1. Confirm you downloaded the correct variant (CUDA/HIP/Vulkan/SYCL)
2. Set **GPU Layers** to a value greater than `0`
3. Verify GPU drivers are installed and up to date
4. Review Server Logs for GPU detection messages

### Download fails

1. Check your internet connection
2. Verify that GitHub is reachable
3. Try a different variant
4. Use the **Cancel** button and retry

### Slow chat responses

1. Increase **GPU Layers** if you have a GPU
2. Use a smaller or more quantized model (e.g., Q4 instead of Q8)
3. Reduce **Context Size**
4. Click **Auto-Detect** to optimize your thread count

## Technical Details

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Newtonsoft.Json` | 13.0.3 | JSON serialization |
| `CommunityToolkit.Mvvm` | 8.2.2 | MVVM helpers and relay commands |
| `AvalonEdit` | 6.3.0.90 | Syntax-highlighted code block rendering |

### llama.cpp Integration

Llama Forge integrates with llama.cpp through:

1. **Process Management**: Spawns `llama-server.exe` as a managed child process
2. **HTTP API**: Communicates via llama.cpp's HTTP API:
   - `GET /health` — Server health check and model load status
   - `POST /v1/chat/completions` — OpenAI-compatible streaming chat endpoint
   - `GET /v1/models` — Retrieve loaded model information

## Roadmap

Phase 1 (Current):
- [x] WPF UI with dark/light theme support
- [x] Server management with real-time logs
- [x] Web-based chat via llama.cpp's built-in WebUI (launched from the app)
- [x] Multi-variant support (CPU, CUDA, Vulkan, ROCm, SYCL)
- [x] Automatic llama.cpp updates with progress and cancel
- [x] Settings persistence
- [x] Startup welcome screen
- [x] WebUI integration with browser launch
- [x] Loading indicator during model initialization
- [x] Model metadata display (architecture, parameter count, etc.)
- [x] Configurable system prompt, temperature, and chat history

Phase 2 (Future):
- [ ] Model download manager (download GGUF files from within the app)
- [ ] Preset server configurations
- [ ] Multiple simultaneous server instances
- [ ] Model quantization tools

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [llama.cpp](https://github.com/ggerganov/llama.cpp) — The C++ LLM inference engine powering this app
- [Georgi Gerganov](https://github.com/ggerganov) — Creator of llama.cpp
- The open-source AI community

---

**Note**: Llama Forge is a GUI wrapper. All AI inference is performed by llama.cpp. Model quality and performance depend on the underlying llama.cpp implementation and the models you use.
