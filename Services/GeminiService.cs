using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SnapMini.Services
{
    /// <summary>
    /// Service for interacting with Google Gemini REST API.
    /// Supports dynamic model selection (gemini-2.5-flash, gemini-1.5-flash, gemini-1.5-pro) and custom system prompts.
    /// </summary>
    public static class GeminiService
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

                    if (doc.RootElement.TryGetProperty("GeminiApiKey", out var keyProperty))
                    {
                        string? keyFromFile = keyProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(keyFromFile) && keyFromFile != "your_actual_gemini_api_key_here")
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

            string? envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                return envKey;
            }

            throw new InvalidOperationException(
                "Gemini API Key missing! Please set your key inside appsettings.json or open Settings (⚙️).");
        }

        public static async Task<string> GetAnswerAsync(string inputText, string modelName = "gemini-2.5-flash", string? customPrompt = null)
        {
            string apiKey = GetApiKey();

            string promptHeader = string.IsNullOrWhiteSpace(customPrompt) 
                ? AIService.DefaultSystemPrompt 
                : customPrompt;

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = promptHeader + "\n\n---\n" + inputText + "\n---"
                            }
                        }
                    }
                }
            };

            string targetModel = string.IsNullOrWhiteSpace(modelName) ? "gemini-2.5-flash" : modelName;
            string endpointUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{targetModel}:generateContent?key={apiKey}";

            string jsonRequestBody = JsonSerializer.Serialize(payload);
            using var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Http.PostAsync(endpointUrl, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Gemini API error ({response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textProp))
                    {
                        return textProp.GetString() ?? "No text response generated.";
                    }
                }
            }

            return "Could not parse response from Gemini API.";
        }
    }
}
