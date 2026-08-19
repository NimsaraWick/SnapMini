using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SnapMini.Services
{
    /// <summary>
    /// Service for interacting with Groq API (OpenAI-compatible chat completions).
    /// Supports dynamic model selection (llama-3.3-70b-versatile, deepseek-r1-distill-llama-70b, mixtral-8x7b-32768, gemma2-9b-it).
    /// </summary>
    public static class GroqService
    {
        private static readonly HttpClient Http = new HttpClient();

        private static string GetApiKey()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(jsonText);

                    if (doc.RootElement.TryGetProperty("GroqApiKey", out var keyProperty))
                    {
                        string? keyFromFile = keyProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(keyFromFile) && keyFromFile != "your_actual_groq_api_key_here")
                        {
                            return keyFromFile;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error reading appsettings.json: {ex.Message}");
                }
            }

            string? envKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                return envKey;
            }

            throw new InvalidOperationException(
                "Groq API Key missing! Please set your key inside appsettings.json or open Settings (⚙️).");
        }

        public static async Task<string> GetChatResponseAsync(System.Collections.Generic.List<AIService.ChatMessage> messages, string modelName = "llama-3.3-70b-versatile", string? customPrompt = null)
        {
            string apiKey = GetApiKey();
            string promptHeader = string.IsNullOrWhiteSpace(customPrompt) ? AIService.DefaultSystemPrompt : customPrompt;
            string targetModel = string.IsNullOrWhiteSpace(modelName) ? "llama-3.3-70b-versatile" : modelName;

            var msgsList = new System.Collections.Generic.List<object>
            {
                new { role = "system", content = promptHeader }
            };

            foreach (var msg in messages)
            {
                msgsList.Add(new
                {
                    role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                    content = msg.Content
                });
            }

            var payload = new
            {
                model = targetModel,
                max_tokens = 1000,
                messages = msgsList
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            string jsonBody = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Http.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Groq API error ({response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageObj) &&
                    messageObj.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString() ?? "No text response generated.";
                }
            }

            return "Could not parse response from Groq API.";
        }

        public static async Task<string> GetAnswerAsync(string inputText, string modelName = "llama-3.3-70b-versatile", string? customPrompt = null)
        {
            return await GetChatResponseAsync(new System.Collections.Generic.List<AIService.ChatMessage>
            {
                new AIService.ChatMessage { Role = "user", Content = inputText }
            }, modelName, customPrompt);
        }
    }
}
