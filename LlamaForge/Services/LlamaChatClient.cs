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

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
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

                        if (data == "[DONE]")
                            break;

                        try
                        {
                            var json = JsonConvert.DeserializeObject<dynamic>(data);
                            if (json?.choices != null && json.choices.Count > 0)
                            {
                                var delta = json.choices[0].delta;
                                if (delta?.content != null)
                                {
                                    string chunk = delta.content;
                                    fullResponse.Append(chunk);
                                    ResponseChunkReceived?.Invoke(this, chunk);
                                }
                            }
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
                ErrorOccurred?.Invoke(this, $"Streaming error: {ex.Message}");
            }

            return fullResponse.ToString();
        }

        private async Task<string> SendNonStreamingRequestAsync(StringContent content)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<dynamic>(responseBody);

                if (json?.choices != null && json.choices.Count > 0)
                {
                    return json.choices[0].message.content;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Request error: {ex.Message}");
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
