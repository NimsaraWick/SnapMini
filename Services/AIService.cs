using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SnapMini.Services
{
    /// <summary>
    /// AI Dispatcher Service. Selects between Gemini and Groq based on the "Provider" setting in appsettings.json.
    /// </summary>
    public static class AIService
    {
        public static string CurrentProviderName { get; private set; } = "Gemini";
        public static string CurrentModelDisplayName { get; private set; } = "Gemini 2.5 Flash";

        public static async Task<string> GetAnswerAsync(string inputText)
        {
            string provider = ReadProviderFromConfig();

            if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                CurrentProviderName = "Groq";
                CurrentModelDisplayName = "Groq Llama 3.3 70B";
                return await GroqService.GetAnswerAsync(inputText);
            }
            else
            {
                CurrentProviderName = "Gemini";
                CurrentModelDisplayName = "Gemini 2.5 Flash";
                return await GeminiService.GetAnswerAsync(inputText);
            }
        }

        private static string ReadProviderFromConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(jsonText);
                    if (doc.RootElement.TryGetProperty("Provider", out var providerProp))
                    {
                        string? provider = providerProp.GetString();
                        if (!string.IsNullOrWhiteSpace(provider)) return provider;
                    }
                }
                catch { }
            }

            return "Gemini";
        }
    }
}
