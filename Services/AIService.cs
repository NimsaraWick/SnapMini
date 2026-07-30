using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SnapMini.Services
{
    /// <summary>
    /// AI Dispatcher & Configuration Manager. 
    /// Manages model presets, API Keys, system prompt customization, and dispatches queries to Gemini or Groq.
    /// </summary>
    public static class AIService
    {
        public const string DefaultSystemPrompt = "The following text was captured from the user's screen or selected text. It may be a question, code snippet, or problem statement. Provide a direct, concise, and accurate answer. If it is a multiple-choice question, state the correct option first.";

        public static string CurrentProviderName { get; private set; } = "Gemini";
        public static string CurrentModelDisplayName { get; private set; } = "Gemini 2.5 Flash";

        public static async Task<string> GetAnswerAsync(string inputText)
        {
            var settings = ReadSettings();

            string provider = settings.Provider;
            string systemPrompt = string.IsNullOrWhiteSpace(settings.SystemPrompt) ? DefaultSystemPrompt : settings.SystemPrompt;

            if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                CurrentProviderName = "Groq";
                string groqModel = string.IsNullOrWhiteSpace(settings.GroqModel) ? "llama-3.3-70b-versatile" : settings.GroqModel;
                CurrentModelDisplayName = GetModelDisplayName("Groq", groqModel);
                return await GroqService.GetAnswerAsync(inputText, groqModel, systemPrompt);
            }
            else
            {
                CurrentProviderName = "Gemini";
                string geminiModel = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel;
                CurrentModelDisplayName = GetModelDisplayName("Gemini", geminiModel);
                return await GeminiService.GetAnswerAsync(inputText, geminiModel, systemPrompt);
            }
        }

        public static string GetModelDisplayName(string provider, string modelId)
        {
            return provider switch
            {
                "Groq" => modelId switch
                {
                    "llama-3.3-70b-versatile" => "Groq Llama 3.3 70B",
                    "deepseek-r1-distill-llama-70b" => "Groq DeepSeek R1 70B",
                    "mixtral-8x7b-32768" => "Groq Mixtral 8x7B",
                    "gemma2-9b-it" => "Groq Gemma 2 9B",
                    _ => $"Groq ({modelId})"
                },
                _ => modelId switch
                {
                    "gemini-2.5-flash" => "Gemini 2.5 Flash",
                    "gemini-1.5-flash" => "Gemini 1.5 Flash",
                    "gemini-1.5-pro" => "Gemini 1.5 Pro",
                    _ => $"Gemini ({modelId})"
                }
            };
        }

        public class AISettings
        {
            public string Provider { get; set; } = "Gemini";
            public string GeminiModel { get; set; } = "gemini-2.5-flash";
            public string GroqModel { get; set; } = "llama-3.3-70b-versatile";
            public string GeminiApiKey { get; set; } = "";
            public string GroqApiKey { get; set; } = "";
            public string SystemPrompt { get; set; } = DefaultSystemPrompt;
        }

        public static AISettings ReadSettings()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(jsonText);
                    var root = doc.RootElement;

                    var settings = new AISettings();

                    if (root.TryGetProperty("Provider", out var p)) settings.Provider = p.GetString() ?? "Gemini";
                    if (root.TryGetProperty("GeminiModel", out var gm)) settings.GeminiModel = gm.GetString() ?? "gemini-2.5-flash";
                    if (root.TryGetProperty("GroqModel", out var qm)) settings.GroqModel = qm.GetString() ?? "llama-3.3-70b-versatile";
                    if (root.TryGetProperty("GeminiApiKey", out var gk)) settings.GeminiApiKey = gk.GetString() ?? "";
                    if (root.TryGetProperty("GroqApiKey", out var qk)) settings.GroqApiKey = qk.GetString() ?? "";
                    if (root.TryGetProperty("SystemPrompt", out var sp)) settings.SystemPrompt = sp.GetString() ?? DefaultSystemPrompt;

                    return settings;
                }
                catch { }
            }

            return new AISettings();
        }

        public static void SaveSettings(AISettings settings)
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            try
            {
                var payload = new
                {
                    Provider = settings.Provider,
                    GeminiModel = settings.GeminiModel,
                    GroqModel = settings.GroqModel,
                    GeminiApiKey = settings.GeminiApiKey,
                    GroqApiKey = settings.GroqApiKey,
                    SystemPrompt = settings.SystemPrompt
                };

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
