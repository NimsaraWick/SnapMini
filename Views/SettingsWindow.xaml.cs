using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using SnapMini.Services;

namespace SnapMini.Views
{
    /// <summary>
    /// Code-behind for SettingsWindow. Handles provider switching, model preset dropdowns,
    /// API key input, and custom system prompt editing.
    /// </summary>
    public partial class SettingsWindow : Window
    {
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

            if (settings.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                RadioGroq.IsChecked = true;
                PopulateModels(_groqModels, settings.GroqModel);
            }
            else
            {
                RadioGemini.IsChecked = true;
                PopulateModels(_geminiModels, settings.GeminiModel);
            }

            GeminiKeyBox.Text = settings.GeminiApiKey;
            GroqKeyBox.Text = settings.GroqApiKey;
            SystemPromptBox.Text = string.IsNullOrWhiteSpace(settings.SystemPrompt) ? AIService.DefaultSystemPrompt : settings.SystemPrompt;
        }

        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            if (ModelCombo == null) return;

            if (RadioGroq.IsChecked == true)
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
                bool isGroq = RadioGroq.IsChecked == true;

                settings.Provider = isGroq ? "Groq" : "Gemini";

                if (ModelCombo.SelectedItem is ModelOption selectedModel)
                {
                    if (isGroq) settings.GroqModel = selectedModel.ModelId;
                    else settings.GeminiModel = selectedModel.ModelId;
                }

                settings.GeminiApiKey = GeminiKeyBox.Text.Trim();
                settings.GroqApiKey = GroqKeyBox.Text.Trim();
                settings.SystemPrompt = SystemPromptBox.Text.Trim();

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

        private void ModelCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
