using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LlamaForge.Models;
using Newtonsoft.Json;

namespace LlamaForge.Services
{
    public class LlamaChatClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public event EventHandler<string>? ResponseChunkReceived;
        public event EventHandler<string>? ErrorOccurred;

        public LlamaChatClient(string host, int port)
        {
            _baseUrl = $"http://{host}:{port}";
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        public string GetBaseUrl() => _baseUrl;

        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetModelInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return content;
                }
                return $"Failed to get model info: HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Error getting model info: {ex.Message}";
            }
        }

        public async Task<string> TestChatEndpointAsync()
        {
            try
            {
                // Send a minimal test request
                var testMessage = new[]
                {
                    new { role = "user", content = "Hi" }
                };

                var requestBody = new
                {
                    messages = testMessage,
                    max_tokens = 10,
                    stream = false
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                return $"Status: {(int)response.StatusCode} {response.StatusCode}\nResponse: {responseBody}";
            }
            catch (Exception ex)
            {
                return $"Test failed: {ex.Message}";
            }
        }

        public async Task<string> SendChatMessageAsync(List<ChatMessage> messages, bool stream = true)
        {
            try
            {
                var requestBody = new
                {
                    messages = messages.Select(m => new
                    {
                        role = m.Role,
                        content = m.Content
                    }),
                    stream = stream,
                    temperature = 0.7,
                    max_tokens = 2048
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");

                if (stream)
                {
                    return await SendStreamingRequestAsync(content);
                }
                else
                {
                    return await SendNonStreamingRequestAsync(content);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error sending message: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> SendStreamingRequestAsync(StringContent content)
        {
            var fullResponse = new StringBuilder();
            int lineCount = 0;
            int chunkCount = 0;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
                {
                    Content = content
                };

                ErrorOccurred?.Invoke(this, $"[Chat] Sending POST to {_baseUrl}/v1/chat/completions");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                ErrorOccurred?.Invoke(this, $"[Chat] HTTP Status: {(int)response.StatusCode} {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorOccurred?.Invoke(this, $"HTTP {(int)response.StatusCode} error: {errorContent}");
                    return string.Empty;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                ErrorOccurred?.Invoke(this, $"[Chat] Starting to read response stream...");

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineCount++;
                    if (lineCount <= 3 || lineCount % 10 == 0)
                    {
                        ErrorOccurred?.Invoke(this, $"[Chat] Line {lineCount}: {(line.Length > 100 ? line.Substring(0, 100) + "..." : line)}");
                    }

                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        if (data == "[DONE]")
                        {
                            ErrorOccurred?.Invoke(this, $"[Chat] Received [DONE] signal. Total chunks: {chunkCount}");
                            break;
                        }

                        try
                        {
                            var json = JsonConvert.DeserializeObject<dynamic>(data);
                            if (json?.choices != null && json.choices.Count > 0)
                            {
                                var delta = json.choices[0].delta;
                                if (delta?.content != null)
                                {
                                    string chunk = delta.content;
                                    chunkCount++;
                                    fullResponse.Append(chunk);
                                    ResponseChunkReceived?.Invoke(this, chunk);

                                    if (chunkCount == 1)
                                    {
                                        ErrorOccurred?.Invoke(this, $"[Chat] First chunk received: '{chunk}'");
                                    }
                                }
                            }
                        }
                        catch (Exception parseEx)
                        {
                            ErrorOccurred?.Invoke(this, $"JSON parse error: {parseEx.Message} | Data: {data}");
                        }
                    }
                }

                ErrorOccurred?.Invoke(this, $"[Chat] Stream complete. Total lines: {lineCount}, chunks: {chunkCount}, response length: {fullResponse.Length}");
            }
            catch (HttpRequestException httpEx)
            {
                ErrorOccurred?.Invoke(this, $"HTTP request error: {httpEx.Message}. Is the server running and accessible at {_baseUrl}?");
            }
            catch (TaskCanceledException)
            {
                ErrorOccurred?.Invoke(this, "Request timed out. The model might be too large or slow to respond.");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Streaming error: {ex.GetType().Name} - {ex.Message}");
            }

            return fullResponse.ToString();
        }

        private async Task<string> SendNonStreamingRequestAsync(StringContent content)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorOccurred?.Invoke(this, $"HTTP {(int)response.StatusCode} error: {errorContent}");
                    return string.Empty;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<dynamic>(responseBody);

                if (json?.choices != null && json.choices.Count > 0)
                {
                    return json.choices[0].message.content;
                }

                ErrorOccurred?.Invoke(this, "Response received but no content in choices array");
                return string.Empty;
            }
            catch (HttpRequestException httpEx)
            {
                ErrorOccurred?.Invoke(this, $"HTTP request error: {httpEx.Message}. Is the server running and accessible at {_baseUrl}?");
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                ErrorOccurred?.Invoke(this, "Request timed out. The model might be too large or slow to respond.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Request error: {ex.GetType().Name} - {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<string> SendCompletionAsync(string prompt, bool stream = true)
        {
            try
            {
                var requestBody = new
                {
                    prompt = prompt,
                    stream = stream,
                    temperature = 0.7,
                    max_tokens = 2048
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");

                if (stream)
                {
                    return await SendStreamingCompletionAsync(content);
                }
                else
                {
                    var response = await _httpClient.PostAsync($"{_baseUrl}/completion", content);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<dynamic>(responseBody);

                    return json?.content ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error sending completion: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> SendStreamingCompletionAsync(StringContent content)
        {
            var fullResponse = new StringBuilder();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/completion")
                {
                    Content = content
                };

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        try
                        {
                            var json = JsonConvert.DeserializeObject<dynamic>(data);
                            if (json?.content != null)
                            {
                                string chunk = json.content;
                                fullResponse.Append(chunk);
                                ResponseChunkReceived?.Invoke(this, chunk);
                            }

                            if (json?.stop == true)
                                break;
                        }
                        catch
                        {
                            // Skip malformed JSON chunks
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Streaming completion error: {ex.Message}");
            }

            return fullResponse.ToString();
        }
    }
}
