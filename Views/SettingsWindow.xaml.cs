using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using SnapMini.Services;

namespace SnapMini.Views
{
    /// <summary>
    /// Code-behind for SettingsWindow. Handles provider switching (Gemini, Groq, OpenRouter),
    /// model preset dropdowns, hidden API key PasswordBoxes with eye toggles, custom system prompt editing,
    /// and auto-close timer duration.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private bool _isGeminiVisible = false;
        private bool _isGroqVisible = false;
        private bool _isOpenRouterVisible = false;

        private readonly List<ModelOption> _geminiModels = new List<ModelOption>
        {
            new ModelOption("Gemini 2.5 Flash (Recommended)", "gemini-2.5-flash"),
            new ModelOption("Gemini 1.5 Flash (Ultra Fast)", "gemini-1.5-flash"),
            new ModelOption("Gemini 1.5 Pro (Deep Reasoning)", "gemini-1.5-pro")
        };

        private readonly List<ModelOption> _groqModels = new List<ModelOption>
        {
            new ModelOption("Llama 3.3 70B Versatile", "llama-3.3-70b-versatile"),
            new ModelOption("DeepSeek R1 Distill 70B", "deepseek-r1-distill-llama-70b"),
            new ModelOption("Mixtral 8x7B", "mixtral-8x7b-32768"),
            new ModelOption("Gemma 2 9B", "gemma2-9b-it")
        };

        private readonly List<ModelOption> _openRouterModels = new List<ModelOption>
        {
            new ModelOption("OpenAI GPT-OSS 20B (Free)", "openai/gpt-oss-20b:free"),
            new ModelOption("NVIDIA Nemotron 3 Ultra 550B (Free)", "nvidia/nemotron-3-ultra-550b-a55b:free"),
            new ModelOption("NVIDIA Nemotron 3 Super 120B (Free)", "nvidia/nemotron-3-super-120b-a12b:free")
        };

        private class ModelOption
        {
            public string DisplayName { get; }
            public string ModelId { get; }

            public ModelOption(string displayName, string modelId)
            {
                DisplayName = displayName;
                ModelId = modelId;
            }

            public override string ToString() => DisplayName;
        }

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = AIService.ReadSettings();

            if (settings.Provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                RadioOpenRouter.IsChecked = true;
                PopulateModels(_openRouterModels, settings.OpenRouterModel);
            }
            else if (settings.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                RadioGroq.IsChecked = true;
                PopulateModels(_groqModels, settings.GroqModel);
            }
            else
            {
                RadioGemini.IsChecked = true;
                PopulateModels(_geminiModels, settings.GeminiModel);
            }

            GeminiKeyPass.Password = settings.GeminiApiKey;
            GeminiKeyText.Text = settings.GeminiApiKey;

            GroqKeyPass.Password = settings.GroqApiKey;
            GroqKeyText.Text = settings.GroqApiKey;

            OpenRouterKeyPass.Password = settings.OpenRouterApiKey;
            OpenRouterKeyText.Text = settings.OpenRouterApiKey;

            SystemPromptBox.Text = string.IsNullOrWhiteSpace(settings.SystemPrompt) ? AIService.DefaultSystemPrompt : settings.SystemPrompt;
            AutoCloseBox.Text = settings.AutoCloseSeconds >= 0 ? settings.AutoCloseSeconds.ToString() : "25";
        }

        private void ToggleGeminiEye_Click(object sender, MouseButtonEventArgs e)
        {
            _isGeminiVisible = !_isGeminiVisible;
            if (_isGeminiVisible)
            {
                GeminiKeyText.Text = GeminiKeyPass.Password;
                GeminiKeyPass.Visibility = Visibility.Collapsed;
                GeminiKeyText.Visibility = Visibility.Visible;
            }
            else
            {
                GeminiKeyPass.Password = GeminiKeyText.Text;
                GeminiKeyText.Visibility = Visibility.Collapsed;
                GeminiKeyPass.Visibility = Visibility.Visible;
            }
        }

        private void ToggleGroqEye_Click(object sender, MouseButtonEventArgs e)
        {
            _isGroqVisible = !_isGroqVisible;
            if (_isGroqVisible)
            {
                GroqKeyText.Text = GroqKeyPass.Password;
                GroqKeyPass.Visibility = Visibility.Collapsed;
                GroqKeyText.Visibility = Visibility.Visible;
            }
            else
            {
                GroqKeyPass.Password = GroqKeyText.Text;
                GroqKeyText.Visibility = Visibility.Collapsed;
                GroqKeyPass.Visibility = Visibility.Visible;
            }
        }

        private void ToggleOpenRouterEye_Click(object sender, MouseButtonEventArgs e)
        {
            _isOpenRouterVisible = !_isOpenRouterVisible;
            if (_isOpenRouterVisible)
            {
                OpenRouterKeyText.Text = OpenRouterKeyPass.Password;
                OpenRouterKeyPass.Visibility = Visibility.Collapsed;
                OpenRouterKeyText.Visibility = Visibility.Visible;
            }
            else
            {
                OpenRouterKeyPass.Password = OpenRouterKeyText.Text;
                OpenRouterKeyText.Visibility = Visibility.Collapsed;
                OpenRouterKeyPass.Visibility = Visibility.Visible;
            }
        }

        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            if (ModelCombo == null) return;

            if (RadioOpenRouter.IsChecked == true)
            {
                PopulateModels(_openRouterModels, "openai/gpt-oss-20b:free");
            }
            else if (RadioGroq.IsChecked == true)
            {
                PopulateModels(_groqModels, "llama-3.3-70b-versatile");
            }
            else
            {
                PopulateModels(_geminiModels, "gemini-2.5-flash");
            }
        }

        private void PopulateModels(List<ModelOption> options, string selectedId)
        {
            ModelCombo.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                ModelCombo.Items.Add(options[i]);
                if (options[i].ModelId.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }
            ModelCombo.SelectedIndex = selectedIndex;
        }

        private void ResetPrompt_Click(object sender, MouseButtonEventArgs e)
        {
            SystemPromptBox.Text = AIService.DefaultSystemPrompt;
        }

        private void Save_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var settings = AIService.ReadSettings();

                if (RadioOpenRouter.IsChecked == true) settings.Provider = "OpenRouter";
                else if (RadioGroq.IsChecked == true) settings.Provider = "Groq";
                else settings.Provider = "Gemini";

                if (ModelCombo.SelectedItem is ModelOption selectedModel)
                {
                    if (settings.Provider == "OpenRouter") settings.OpenRouterModel = selectedModel.ModelId;
                    else if (settings.Provider == "Groq") settings.GroqModel = selectedModel.ModelId;
                    else settings.GeminiModel = selectedModel.ModelId;
                }

                settings.GeminiApiKey = (_isGeminiVisible ? GeminiKeyText.Text : GeminiKeyPass.Password).Trim();
                settings.GroqApiKey = (_isGroqVisible ? GroqKeyText.Text : GroqKeyPass.Password).Trim();
                settings.OpenRouterApiKey = (_isOpenRouterVisible ? OpenRouterKeyText.Text : OpenRouterKeyPass.Password).Trim();
                settings.SystemPrompt = SystemPromptBox.Text.Trim();

                if (int.TryParse(AutoCloseBox.Text.Trim(), out int timerSec) && timerSec >= 0)
                {
                    settings.AutoCloseSeconds = timerSec;
                }
                else
                {
                    settings.AutoCloseSeconds = 25;
                }

                AIService.SaveSettings(settings);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save settings:\n{ex.Message}", "SnapMini Settings Error");
            }
        }

        private void Cancel_Click(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void Close_Click(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
