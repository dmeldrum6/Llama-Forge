using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LlamaForge.Models;
using Newtonsoft.Json;

namespace LlamaForge.Services
{
    public class LlamaChatClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _disposed;

        /// <summary>Fires when actual errors occur during HTTP communication.</summary>
        public event EventHandler<string>? ErrorOccurred;

        /// <summary>Fires for diagnostic/informational log messages.</summary>
        public event EventHandler<string>? LogReceived;

        /// <summary>Fires for each streamed response chunk received from the server.</summary>
        public event EventHandler<string>? ResponseChunkReceived;

        public LlamaChatClient(string host, int port)
        {
            _baseUrl = $"http://{host}:{port}";
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LlamaForge");
        }

        public string GetBaseUrl() => _baseUrl;

        public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a human-readable health status string for diagnostic logging.
        /// Distinguishes between "loading model" (HTTP 503), "ready" (HTTP 200),
        /// connection refused (process not listening), and other errors.
        /// </summary>
        public async Task<string> GetHealthStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync($"{_baseUrl}/health", cts.Token);
                var body = (await response.Content.ReadAsStringAsync()).Trim();
                return $"HTTP {(int)response.StatusCode} — {body}";
            }
            catch (OperationCanceledException)
            {
                return "timed out waiting for response";
            }
            catch (HttpRequestException ex)
            {
                return $"connection error ({ex.Message})";
            }
            catch (Exception ex)
            {
                return $"error: {ex.Message}";
            }
        }

        public async Task<string> GetModelInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models", cancellationToken);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                return $"Failed to get model info: HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Error getting model info: {ex.Message}";
            }
        }

        public async Task<ModelInfo?> GetDetailedModelInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var modelsResponse = JsonConvert.DeserializeObject<ModelsResponse>(content);

                if (modelsResponse?.Data == null || modelsResponse.Data.Count == 0)
                    return null;

                var modelData = modelsResponse.Data[0];
                var modelInfo = new ModelInfo
                {
                    Id = modelData.Id ?? string.Empty,
                    Object = modelData.Object ?? string.Empty,
                    Created = modelData.Created,
                    OwnedBy = modelData.OwnedBy ?? string.Empty
                };

                if (modelData.Meta != null)
                {
                    modelInfo.Meta = new ModelMetadata
                    {
                        VocabType = modelData.Meta.VocabType ?? 0,
                        VocabSize = modelData.Meta.NVocab ?? 0,
                        TrainingContextLength = modelData.Meta.NCtxTrain ?? 0,
                        EmbeddingDimensions = modelData.Meta.NEmbd ?? 0,
                        ParameterCount = modelData.Meta.NParams ?? 0,
                        ModelSize = modelData.Meta.Size ?? 0
                    };

                    modelInfo.Architecture = modelData.Meta.Architecture;
                    modelInfo.Name = modelData.Meta.ModelName;
                    modelInfo.Version = modelData.Meta.ModelVersion;
                    modelInfo.Finetune = modelData.Meta.Finetune;
                    modelInfo.License = modelData.Meta.License;
                    modelInfo.SizeLabel = modelData.Meta.SizeLabel;
                }

                return modelInfo;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error getting detailed model info: {ex.Message}");
                return null;
            }
        }

        public async Task<string> TestChatEndpointAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new ChatCompletionRequest
                {
                    Messages = new[] { new MessageDto { Role = "user", Content = "Hi" } },
                    MaxTokens = 10,
                    Stream = false
                };

                var content = Serialize(requestBody);
                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync();

                return $"Status: {(int)response.StatusCode} {response.StatusCode}\nResponse: {responseBody}";
            }
            catch (Exception ex)
            {
                return $"Test failed: {ex.Message}";
            }
        }

        public async Task<string> SendChatMessageAsync(
            List<ChatMessage> messages,
            bool stream = true,
            double temperature = 0.7,
            int maxTokens = 2048,
            int maxHistoryMessages = 20,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Trim history to avoid exceeding context window
                var trimmedMessages = messages.Count > maxHistoryMessages
                    ? messages.TakeLast(maxHistoryMessages).ToList()
                    : messages;

                var requestBody = new ChatCompletionRequest
                {
                    Messages = trimmedMessages.Select(m => new MessageDto
                    {
                        Role = m.Role,
                        Content = m.Content
                    }).ToArray(),
                    Stream = stream,
                    Temperature = temperature,
                    MaxTokens = maxTokens
                };

                var content = Serialize(requestBody);

                if (stream)
                    return await SendStreamingRequestAsync(content, cancellationToken);
                else
                    return await SendNonStreamingRequestAsync(content, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error sending message: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> SendStreamingRequestAsync(StringContent content, CancellationToken cancellationToken)
        {
            var fullResponse = new StringBuilder();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
                {
                    Content = content
                };

                LogReceived?.Invoke(this, $"Sending streaming request to {_baseUrl}/v1/chat/completions");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorOccurred?.Invoke(this, $"HTTP {(int)response.StatusCode}: {errorContent}");
                    return string.Empty;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!line.StartsWith("data: "))
                        continue;

                    var data = line.Substring(6);

                    if (data == "[DONE]")
                        break;

                    try
                    {
                        var chunk = JsonConvert.DeserializeObject<ChatCompletionChunk>(data);
                        var chunkContent = chunk?.Choices?[0]?.Delta?.Content;
                        if (chunkContent != null)
                        {
                            fullResponse.Append(chunkContent);
                            ResponseChunkReceived?.Invoke(this, chunkContent);
                        }
                    }
                    catch (JsonException ex)
                    {
                        LogReceived?.Invoke(this, $"Skipping malformed SSE chunk: {ex.Message}");
                    }
                }

                LogReceived?.Invoke(this, $"Stream complete. Response length: {fullResponse.Length} chars");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                ErrorOccurred?.Invoke(this, "Request timed out. The model may be too large or slow to respond.");
            }
            catch (OperationCanceledException)
            {
                // Propagate so callers can distinguish cancellation from error
                throw;
            }
            catch (HttpRequestException ex)
            {
                ErrorOccurred?.Invoke(this, $"HTTP request error: {ex.Message}. Is the server running at {_baseUrl}?");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Streaming error: {ex.GetType().Name} - {ex.Message}");
            }

            return fullResponse.ToString();
        }

        private async Task<string> SendNonStreamingRequestAsync(StringContent content, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorOccurred?.Invoke(this, $"HTTP {(int)response.StatusCode}: {errorContent}");
                    return string.Empty;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var completion = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseBody);

                return completion?.Choices?[0]?.Message?.Content ?? string.Empty;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                ErrorOccurred?.Invoke(this, "Request timed out. The model may be too large or slow to respond.");
                return string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                ErrorOccurred?.Invoke(this, $"HTTP request error: {ex.Message}. Is the server running at {_baseUrl}?");
                return string.Empty;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Request error: {ex.GetType().Name} - {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<string> SendCompletionAsync(string prompt, bool stream = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    prompt,
                    stream,
                    temperature = 0.7,
                    max_tokens = 2048
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");

                if (stream)
                    return await SendStreamingCompletionAsync(content, cancellationToken);

                var response = await _httpClient.PostAsync($"{_baseUrl}/completion", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<CompletionResponse>(responseBody);
                return json?.Content ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error sending completion: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> SendStreamingCompletionAsync(StringContent content, CancellationToken cancellationToken)
        {
            var fullResponse = new StringBuilder();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/completion")
                {
                    Content = content
                };

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!line.StartsWith("data: "))
                        continue;

                    var data = line.Substring(6);

                    try
                    {
                        var chunk = JsonConvert.DeserializeObject<CompletionChunk>(data);
                        if (chunk?.Content != null)
                        {
                            fullResponse.Append(chunk.Content);
                            ResponseChunkReceived?.Invoke(this, chunk.Content);
                        }

                        if (chunk?.Stop == true)
                            break;
                    }
                    catch (JsonException)
                    {
                        // Skip malformed JSON chunks
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Streaming completion error: {ex.Message}");
            }

            return fullResponse.ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _httpClient.Dispose();
        }

        // ── Serialization helpers ──────────────────────────────────────────────

        private static StringContent Serialize(object obj) =>
            new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");

        // ── Request / response DTOs ────────────────────────────────────────────

        private class ChatCompletionRequest
        {
            [JsonProperty("messages")]
            public MessageDto[]? Messages { get; set; }

            [JsonProperty("stream")]
            public bool Stream { get; set; }

            [JsonProperty("temperature")]
            public double Temperature { get; set; }

            [JsonProperty("max_tokens")]
            public int MaxTokens { get; set; }
        }

        private class MessageDto
        {
            [JsonProperty("role")]
            public string Role { get; set; } = string.Empty;

            [JsonProperty("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class ChatCompletionChunk
        {
            [JsonProperty("choices")]
            public List<ChunkChoice>? Choices { get; set; }
        }

        private class ChunkChoice
        {
            [JsonProperty("delta")]
            public DeltaContent? Delta { get; set; }
        }

        private class DeltaContent
        {
            [JsonProperty("content")]
            public string? Content { get; set; }
        }

        private class ChatCompletionResponse
        {
            [JsonProperty("choices")]
            public List<CompletionChoice>? Choices { get; set; }
        }

        private class CompletionChoice
        {
            [JsonProperty("message")]
            public MessageDto? Message { get; set; }
        }

        private class CompletionChunk
        {
            [JsonProperty("content")]
            public string? Content { get; set; }

            [JsonProperty("stop")]
            public bool Stop { get; set; }
        }

        private class CompletionResponse
        {
            [JsonProperty("content")]
            public string? Content { get; set; }
        }

        // ── /v1/models response DTOs ───────────────────────────────────────────

        private class ModelsResponse
        {
            [JsonProperty("object")]
            public string? Object { get; set; }

            [JsonProperty("data")]
            public List<ModelData>? Data { get; set; }
        }

        private class ModelData
        {
            [JsonProperty("id")]
            public string? Id { get; set; }

            [JsonProperty("object")]
            public string? Object { get; set; }

            [JsonProperty("created")]
            public long Created { get; set; }

            [JsonProperty("owned_by")]
            public string? OwnedBy { get; set; }

            [JsonProperty("meta")]
            public ModelMeta? Meta { get; set; }
        }

        private class ModelMeta
        {
            [JsonProperty("vocab_type")]
            public int? VocabType { get; set; }

            [JsonProperty("n_vocab")]
            public int? NVocab { get; set; }

            [JsonProperty("n_ctx_train")]
            public int? NCtxTrain { get; set; }

            [JsonProperty("n_embd")]
            public int? NEmbd { get; set; }

            [JsonProperty("n_params")]
            public long? NParams { get; set; }

            [JsonProperty("size")]
            public long? Size { get; set; }

            [JsonProperty("general.architecture")]
            public string? Architecture { get; set; }

            [JsonProperty("general.name")]
            public string? ModelName { get; set; }

            [JsonProperty("general.version")]
            public string? ModelVersion { get; set; }

            [JsonProperty("general.finetune")]
            public string? Finetune { get; set; }

            [JsonProperty("general.license")]
            public string? License { get; set; }

            [JsonProperty("general.size_label")]
            public string? SizeLabel { get; set; }
        }
    }
}
