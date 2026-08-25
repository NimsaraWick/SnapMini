using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnapMini.Services;

namespace SnapMini.Views
{
    /// <summary>
    /// Code-behind for SettingsWindow. Handles provider switching (Gemini, Groq, OpenRouter),
    /// model preset dropdowns, hidden API key PasswordBoxes with eye toggles, custom system prompt editing,
    /// custom quick action tags management, live interactive window width/height dimensions, and auto-close timer.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private bool _isGeminiVisible = false;
        private bool _isGroqVisible = false;
        private bool _isOpenRouterVisible = false;

        public bool IsSaved { get; private set; } = false;
        public Action<int, int>? OnSizePreview { get; set; }

        private readonly List<ModelOption> _geminiModels = new List<ModelOption>
        {
            new ModelOption("Gemini 2.5 Flash (Recommended)", "gemini-2.5-flash"),
            new ModelOption("Gemini 1.5 Flash (Ultra Fast)", "gemini-1.5-flash"),
            new ModelOption("Gemini 1.5 Pro (Deep Reasoning)", "gemini-1.5-pro")
        };

        private readonly List<ModelOption> _groqModels = new List<ModelOption>
        {
            new ModelOption("OpenAI GPT-OSS 20B (Ultra Fast)", "openai/gpt-oss-20b"),
            new ModelOption("OpenAI GPT-OSS 120B (High Reasoning)", "openai/gpt-oss-120b"),
            new ModelOption("Qwen 3.6 27B", "qwen/qwen3.6-27b"),
            new ModelOption("Groq Compound", "groq/compound"),
            new ModelOption("Groq Compound Mini", "groq/compound-mini")
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

            // Load Window Size
            int w = settings.WindowWidth > 0 ? settings.WindowWidth : 680;
            int h = settings.WindowHeight > 0 ? settings.WindowHeight : 580;
            WindowWidthBox.Text = w.ToString();
            WindowHeightBox.Text = h.ToString();
            HighlightActiveSizePreset(w, h);

            // Load Custom Quick Action Tags
            LoadQuickActionTags(settings.QuickActions);
        }

        private void LoadQuickActionTags(List<AIService.QuickActionTag>? tags)
        {
            QuickActionsListPanel.Children.Clear();
            var list = (tags != null && tags.Count > 0) ? tags : AIService.GetDefaultQuickActions();
            foreach (var tag in list)
            {
                AddTagRow(tag.Label, tag.Prompt);
            }
        }

        private void AddTagRow(string label = "", string prompt = "")
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });

            var labelBox = new TextBox
            {
                Text = label,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12
            };
            Grid.SetColumn(labelBox, 0);

            var promptBox = new TextBox
            {
                Text = prompt,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(promptBox, 1);

            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Move Up Button
            var upBtn = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 2, 0),
                ToolTip = "Move Up"
            };
            var upIcon = new TextBlock
            {
                Text = "▲",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            upBtn.Child = upIcon;
            upBtn.MouseEnter += (s, e) => { upBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")); upIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")); };
            upBtn.MouseLeave += (s, e) => { upBtn.Background = Brushes.Transparent; upIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")); };
            upBtn.MouseLeftButtonDown += (s, e) =>
            {
                int index = QuickActionsListPanel.Children.IndexOf(rowGrid);
                if (index > 0)
                {
                    QuickActionsListPanel.Children.Remove(rowGrid);
                    QuickActionsListPanel.Children.Insert(index - 1, rowGrid);
                }
            };

            // Move Down Button
            var downBtn = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "Move Down"
            };
            var downIcon = new TextBlock
            {
                Text = "▼",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            downBtn.Child = downIcon;
            downBtn.MouseEnter += (s, e) => { downBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")); downIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")); };
            downBtn.MouseLeave += (s, e) => { downBtn.Background = Brushes.Transparent; downIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")); };
            downBtn.MouseLeftButtonDown += (s, e) =>
            {
                int index = QuickActionsListPanel.Children.IndexOf(rowGrid);
                if (index < QuickActionsListPanel.Children.Count - 1)
                {
                    QuickActionsListPanel.Children.Remove(rowGrid);
                    QuickActionsListPanel.Children.Insert(index + 1, rowGrid);
                }
            };

            // Delete Button
            var delBtn = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Width = 24,
                Height = 24,
                ToolTip = "Delete Tag"
            };
            var delIcon = new TextBlock
            {
                Text = "✕",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            delBtn.Child = delIcon;
            delBtn.MouseEnter += (s, e) => { delBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#451A1A")); };
            delBtn.MouseLeave += (s, e) => { delBtn.Background = Brushes.Transparent; };
            delBtn.MouseLeftButtonDown += (s, e) =>
            {
                QuickActionsListPanel.Children.Remove(rowGrid);
            };

            actionPanel.Children.Add(upBtn);
            actionPanel.Children.Add(downBtn);
            actionPanel.Children.Add(delBtn);

            Grid.SetColumn(actionPanel, 2);

            rowGrid.Children.Add(labelBox);
            rowGrid.Children.Add(promptBox);
            rowGrid.Children.Add(actionPanel);

            QuickActionsListPanel.Children.Add(rowGrid);
        }

        private void AddTag_Click(object sender, MouseButtonEventArgs e)
        {
            AddTagRow("", "");
        }

        private void ResetDefaultTags_Click(object sender, MouseButtonEventArgs e)
        {
            LoadQuickActionTags(AIService.GetDefaultQuickActions());
        }

        #region Size Presets & Live Preview

        private void DimensionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WindowWidthBox == null || WindowHeightBox == null) return;

            if (int.TryParse(WindowWidthBox.Text.Trim(), out int w) && int.TryParse(WindowHeightBox.Text.Trim(), out int h))
            {
                if (w >= 400 && h >= 300)
                {
                    HighlightActiveSizePreset(w, h);
                    OnSizePreview?.Invoke(w, h);
                }
            }
        }

        private void PresetSize_Compact(object sender, MouseButtonEventArgs e)
        {
            SetDimensions(560, 480);
        }

        private void PresetSize_Standard(object sender, MouseButtonEventArgs e)
        {
            SetDimensions(680, 580);
        }

        private void PresetSize_Wide(object sender, MouseButtonEventArgs e)
        {
            SetDimensions(820, 600);
        }

        private void PresetSize_Large(object sender, MouseButtonEventArgs e)
        {
            SetDimensions(960, 720);
        }

        private void PresetSize_FullScreen(object sender, MouseButtonEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            int fsW = (int)workArea.Width - 16;
            int fsH = (int)workArea.Height - 16;
            SetDimensions(fsW, fsH);
        }

        private void ResetDefaultSize_Click(object sender, MouseButtonEventArgs e)
        {
            SetDimensions(680, 580);
        }

        private void SetDimensions(int width, int height)
        {
            WindowWidthBox.Text = width.ToString();
            WindowHeightBox.Text = height.ToString();
            HighlightActiveSizePreset(width, height);
            OnSizePreview?.Invoke(width, height);
        }

        private void HighlightActiveSizePreset(int w, int h)
        {
            ResetPresetHighlights();

            var workArea = SystemParameters.WorkArea;
            int fsW = (int)workArea.Width - 16;
            int fsH = (int)workArea.Height - 16;

            if (w == 560 && h == 480)
            {
                HighlightPill(PillCompact, TextCompact);
            }
            else if (w == 680 && h == 580)
            {
                HighlightPill(PillStandard, TextStandard);
            }
            else if (w == 820 && h == 600)
            {
                HighlightPill(PillWide, TextWide);
            }
            else if (w == 960 && h == 720)
            {
                HighlightPill(PillLarge, TextLarge);
            }
            else if (Math.Abs(w - fsW) <= 20 && Math.Abs(h - fsH) <= 20)
            {
                HighlightPill(PillFullScreen, TextFullScreen);
            }
        }

        private void HighlightPill(Border? pill, TextBlock? text)
        {
            if (pill == null || text == null) return;
            pill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
            pill.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#818CF8"));
            text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            text.FontWeight = FontWeights.SemiBold;
        }

        private void ResetPresetHighlights()
        {
            var defaultBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            var defaultBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            var defaultFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));

            if (PillCompact != null) { PillCompact.Background = defaultBg; PillCompact.BorderBrush = defaultBorder; }
            if (PillStandard != null) { PillStandard.Background = defaultBg; PillStandard.BorderBrush = defaultBorder; }
            if (PillWide != null) { PillWide.Background = defaultBg; PillWide.BorderBrush = defaultBorder; }
            if (PillLarge != null) { PillLarge.Background = defaultBg; PillLarge.BorderBrush = defaultBorder; }
            if (PillFullScreen != null) { PillFullScreen.Background = defaultBg; PillFullScreen.BorderBrush = defaultBorder; }

            if (TextCompact != null) { TextCompact.Foreground = defaultFg; TextCompact.FontWeight = FontWeights.Normal; }
            if (TextStandard != null) { TextStandard.Foreground = defaultFg; TextStandard.FontWeight = FontWeights.Normal; }
            if (TextWide != null) { TextWide.Foreground = defaultFg; TextWide.FontWeight = FontWeights.Normal; }
            if (TextLarge != null) { TextLarge.Foreground = defaultFg; TextLarge.FontWeight = FontWeights.Normal; }
            if (TextFullScreen != null) { TextFullScreen.Foreground = defaultFg; TextFullScreen.FontWeight = FontWeights.Normal; }
        }

        #endregion

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
                PopulateModels(_groqModels, "openai/gpt-oss-20b");
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

                // Save Window Size
                if (int.TryParse(WindowWidthBox.Text.Trim(), out int w) && w >= 400 && w <= 3840)
                {
                    settings.WindowWidth = w;
                }
                else
                {
                    settings.WindowWidth = 680;
                }

                if (int.TryParse(WindowHeightBox.Text.Trim(), out int h) && h >= 300 && h <= 2160)
                {
                    settings.WindowHeight = h;
                }
                else
                {
                    settings.WindowHeight = 580;
                }

                // Save Auto-Close Timer
                if (int.TryParse(AutoCloseBox.Text.Trim(), out int timerSec) && timerSec >= 0)
                {
                    settings.AutoCloseSeconds = timerSec;
                }
                else
                {
                    settings.AutoCloseSeconds = 25;
                }

                // Save Custom Quick Action Tags
                var customTags = new List<AIService.QuickActionTag>();
                foreach (UIElement child in QuickActionsListPanel.Children)
                {
                    if (child is Grid g && g.Children.Count >= 2 && g.Children[0] is TextBox lblBox && g.Children[1] is TextBox prmBox)
                    {
                        string lbl = lblBox.Text.Trim();
                        string prm = prmBox.Text.Trim();
                        if (!string.IsNullOrWhiteSpace(lbl))
                        {
                            customTags.Add(new AIService.QuickActionTag { Label = lbl, Prompt = prm });
                        }
                    }
                }
                settings.QuickActions = customTags;

                AIService.SaveSettings(settings);
                IsSaved = true;
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
