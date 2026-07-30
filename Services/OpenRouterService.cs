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
    /// Service for interacting with OpenRouter API (OpenAI-compatible gateway supporting hundreds of LLMs).
    /// Supports models like deepseek/deepseek-r1:free, meta-llama/llama-3.3-70b-instruct:free, anthropic/claude-3.5-sonnet, etc.
    /// </summary>
    public static class OpenRouterService
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

                    if (doc.RootElement.TryGetProperty("OpenRouterApiKey", out var keyProperty))
                    {
                        string? keyFromFile = keyProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(keyFromFile) && keyFromFile != "your_actual_openrouter_api_key_here")
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

            string? envKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                return envKey;
            }

            throw new InvalidOperationException(
                "OpenRouter API Key missing! Please set your key inside appsettings.json or open Settings (⚙️).");
        }

        /// <summary>
        /// Sends prompt & text to OpenRouter REST API endpoint.
        /// </summary>
        public static async Task<string> GetAnswerAsync(string inputText, string modelName = "deepseek/deepseek-r1:free", string? customPrompt = null)
        {
            string apiKey = GetApiKey();

            string promptHeader = string.IsNullOrWhiteSpace(customPrompt) 
                ? AIService.DefaultSystemPrompt 
                : customPrompt;

            string targetModel = string.IsNullOrWhiteSpace(modelName) ? "deepseek/deepseek-r1:free" : modelName;

            var payload = new
            {
                model = targetModel,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = promptHeader + "\n\n---\n" + inputText + "\n---"
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", "https://github.com/NimsaraWick/SnapMini");
            request.Headers.Add("X-Title", "SnapMini");

            string jsonBody = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Http.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OpenRouter API error ({response.StatusCode}): {responseBody}");
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

            return "Could not parse response from OpenRouter API.";
        }
    }
}
