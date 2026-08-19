using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SnapMini.Services
{
    /// <summary>
    /// AI Dispatcher & Configuration Manager. 
    /// Manages model presets, API Keys, system prompt customization, window position, and auto-close timer duration in appsettings.json.
    /// </summary>
    public static class AIService
    {
        public const string DefaultSystemPrompt = "The following text was captured from the user's screen or selected text. It may be a question, code snippet, or problem statement. Provide a direct, concise, and accurate answer. If it is a multiple-choice question, state the correct option first.";

        public static string CurrentProviderName { get; private set; } = "Gemini";
        public static string CurrentModelDisplayName { get; private set; } = "Gemini 2.5 Flash";

        /// <summary>
        /// Represents a message in a multi-turn conversation.
        /// </summary>
        public class ChatMessage
        {
            public string Role { get; set; } = "user"; // "user" or "assistant"
            public string Content { get; set; } = "";
        }

        /// <summary>
        /// Reads settings and dispatches full multi-turn chat history to the selected AI model provider.
        /// </summary>
        public static async Task<string> GetChatResponseAsync(System.Collections.Generic.List<ChatMessage> messages)
        {
            var settings = ReadSettings();

            string provider = settings.Provider;
            string systemPrompt = string.IsNullOrWhiteSpace(settings.SystemPrompt) ? DefaultSystemPrompt : settings.SystemPrompt;

            if (provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                CurrentProviderName = "OpenRouter";
                string openRouterModel = string.IsNullOrWhiteSpace(settings.OpenRouterModel) ? "openai/gpt-oss-20b:free" : settings.OpenRouterModel;
                CurrentModelDisplayName = GetModelDisplayName("OpenRouter", openRouterModel);
                return await OpenRouterService.GetChatResponseAsync(messages, openRouterModel, systemPrompt);
            }
            else if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                CurrentProviderName = "Groq";
                string groqModel = string.IsNullOrWhiteSpace(settings.GroqModel) ? "llama-3.3-70b-versatile" : settings.GroqModel;
                CurrentModelDisplayName = GetModelDisplayName("Groq", groqModel);
                return await GroqService.GetChatResponseAsync(messages, groqModel, systemPrompt);
            }
            else
            {
                CurrentProviderName = "Gemini";
                string geminiModel = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel;
                CurrentModelDisplayName = GetModelDisplayName("Gemini", geminiModel);
                return await GeminiService.GetChatResponseAsync(messages, geminiModel, systemPrompt);
            }
        }

        /// <summary>
        /// Reads settings and dispatches a single prompt & input text to the selected AI model provider.
        /// </summary>
        public static async Task<string> GetAnswerAsync(string inputText)
        {
            return await GetChatResponseAsync(new System.Collections.Generic.List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = inputText }
            });
        }

        public static string GetModelDisplayName(string provider, string modelId)
        {
            return provider switch
            {
                "OpenRouter" => modelId switch
                {
                    "openai/gpt-oss-20b:free" => "OpenRouter GPT-OSS 20B (Free)",
                    "nvidia/nemotron-3-ultra-550b-a55b:free" => "OpenRouter Nemotron 3 Ultra (Free)",
                    "nvidia/nemotron-3-super-120b-a12b:free" => "OpenRouter Nemotron 3 Super (Free)",
                    _ => $"OpenRouter ({modelId})"
                },
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
            public string OpenRouterModel { get; set; } = "openai/gpt-oss-20b:free";
            public string GeminiApiKey { get; set; } = "";
            public string GroqApiKey { get; set; } = "";
            public string OpenRouterApiKey { get; set; } = "";
            public string SystemPrompt { get; set; } = DefaultSystemPrompt;
            public string WindowPosition { get; set; } = "TopRight";
            public int AutoCloseSeconds { get; set; } = 25;
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
                    if (root.TryGetProperty("OpenRouterModel", out var om)) settings.OpenRouterModel = om.GetString() ?? "openai/gpt-oss-20b:free";
                    if (root.TryGetProperty("GeminiApiKey", out var gk)) settings.GeminiApiKey = gk.GetString() ?? "";
                    if (root.TryGetProperty("GroqApiKey", out var qk)) settings.GroqApiKey = qk.GetString() ?? "";
                    if (root.TryGetProperty("OpenRouterApiKey", out var ok)) settings.OpenRouterApiKey = ok.GetString() ?? "";
                    if (root.TryGetProperty("SystemPrompt", out var sp)) settings.SystemPrompt = sp.GetString() ?? DefaultSystemPrompt;
                    if (root.TryGetProperty("WindowPosition", out var wp)) settings.WindowPosition = wp.GetString() ?? "TopRight";
                    if (root.TryGetProperty("AutoCloseSeconds", out var acs)) settings.AutoCloseSeconds = acs.GetInt32();

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
                    OpenRouterModel = settings.OpenRouterModel,
                    GeminiApiKey = settings.GeminiApiKey,
                    GroqApiKey = settings.GroqApiKey,
                    OpenRouterApiKey = settings.OpenRouterApiKey,
                    SystemPrompt = settings.SystemPrompt,
                    WindowPosition = settings.WindowPosition,
                    AutoCloseSeconds = settings.AutoCloseSeconds
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
