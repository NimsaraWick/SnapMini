using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapMini.Helpers;
using SnapMini.Services;

namespace SnapMini.Views
{
    /// <summary>
    /// Code-behind for AnswerWindow. Includes multi-turn follow-up chat conversation,
    /// dynamic custom quick action tags configured from Settings, screen position picker,
    /// configurable auto-close timer with smooth fade-out, and branding.
    /// </summary>
    public partial class AnswerWindow : Window
    {
        private readonly DispatcherTimer? _timer;
        private int _ticksRemaining = 250;
        private bool _isPaused = false;
        private bool _isClosing = false;
        private bool _isSending = false;
        private bool _isContextExpanded = false;
        private readonly string _initialAnswerText;
        private readonly List<AIService.ChatMessage> _chatHistory = new();

        public AnswerWindow(string questionText, string answerText, string modelName = "")
        {
            InitializeComponent();

            _initialAnswerText = answerText.Trim();
            QuestionBox.Text = questionText.Trim();
            AnswerBox.Document = MarkdownHelper.ToFlowDocument(_initialAnswerText);
            ModelTagText.Text = string.IsNullOrWhiteSpace(modelName) ? AIService.CurrentModelDisplayName : modelName;

            // Seed multi-turn chat history
            _chatHistory.Add(new AIService.ChatMessage { Role = "user", Content = questionText.Trim() });
            _chatHistory.Add(new AIService.ChatMessage { Role = "assistant", Content = _initialAnswerText });

            LoadLogoImage();
            LoadQuickActionTags();
            LoadGoogleTargetsDropdown();

            // Read user's custom AutoCloseSeconds from appsettings.json
            var settings = AIService.ReadSettings();
            int autoCloseSec = settings.AutoCloseSeconds;

            if (autoCloseSec <= 0)
            {
                // Disable auto-close timer completely if set to 0
                AutoCloseProgress.Visibility = Visibility.Collapsed;
                PauseBtn.Visibility = Visibility.Collapsed;
                TimerLabel.Text = "Manual close mode";
            }
            else
            {
                _ticksRemaining = autoCloseSec * 10;
                AutoCloseProgress.Maximum = autoCloseSec * 10;
                AutoCloseProgress.Value = autoCloseSec * 10;

                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _timer.Tick += (sender, e) =>
                {
                    _ticksRemaining--;
                    AutoCloseProgress.Value = _ticksRemaining;
                    TimerLabel.Text = $"Auto closing in {(_ticksRemaining / 10) + 1}s";

                    if (_ticksRemaining <= 0)
                    {
                        _timer.Stop();
                        StartFadeOutAndClose();
                    }
                };

                _timer.Start();
            }
        }

        #region Google Export Target Dropdown

        private void LoadGoogleTargetsDropdown()
        {
            GoogleTargetComboBox.SelectionChanged -= GoogleTargetComboBox_SelectionChanged;
            GoogleTargetComboBox.Items.Clear();

            var settings = AIService.ReadSettings();
            var targets = settings.GoogleExportTargets;

            if (targets == null || targets.Count == 0)
            {
                var defaultItem = new ComboBoxItem
                {
                    Content = "📄 Default Doc",
                    Tag = null,
                    ToolTip = "Default Google Doc"
                };
                GoogleTargetComboBox.Items.Add(defaultItem);
                GoogleTargetComboBox.SelectedIndex = 0;
                GoogleTargetComboBox.SelectionChanged += GoogleTargetComboBox_SelectionChanged;
                return;
            }

            ComboBoxItem? itemToSelect = null;
            foreach (var target in targets)
            {
                string icon = string.Equals(target.Type, "Folder", StringComparison.OrdinalIgnoreCase) ? "📁" : "📄";
                string label = string.IsNullOrWhiteSpace(target.Name) ? (target.Type == "Folder" ? "Drive Folder" : "Google Doc") : target.Name;

                var item = new ComboBoxItem
                {
                    Content = $"{icon} {label}",
                    Tag = target,
                    ToolTip = $"{label}\nType: {(target.Type == "Folder" ? "Google Drive Folder" : "Google Document")}\nLink/ID: {target.UrlOrId}"
                };

                GoogleTargetComboBox.Items.Add(item);

                if (!string.IsNullOrWhiteSpace(settings.LastSelectedGoogleTargetId) && target.Id == settings.LastSelectedGoogleTargetId)
                {
                    itemToSelect = item;
                }
            }

            if (itemToSelect != null)
            {
                GoogleTargetComboBox.SelectedItem = itemToSelect;
            }
            else if (GoogleTargetComboBox.Items.Count > 0)
            {
                GoogleTargetComboBox.SelectedIndex = 0;
            }

            UpdateSaveDocButtonToolTip();
            GoogleTargetComboBox.SelectionChanged += GoogleTargetComboBox_SelectionChanged;
        }

        private void GoogleTargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSaveDocButtonToolTip();

            if (GoogleTargetComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is AIService.GoogleExportTarget target)
            {
                try
                {
                    var settings = AIService.ReadSettings();
                    settings.LastSelectedGoogleTargetId = target.Id;
                    AIService.SaveSettings(settings);
                }
                catch { }
            }
        }

        private void UpdateSaveDocButtonToolTip()
        {
            if (GoogleTargetComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is AIService.GoogleExportTarget target)
            {
                string label = string.IsNullOrWhiteSpace(target.Name) ? (target.Type == "Folder" ? "Drive Folder" : "Google Doc") : target.Name;
                SaveDocBtn.ToolTip = $"Save Q&A to '{label}' (Ctrl+D)";
            }
            else
            {
                SaveDocBtn.ToolTip = "Save Q&A to Google Docs / Drive (Ctrl+D)";
            }
        }

        #endregion

        #region Dynamic Quick Action Tags

        private void LoadQuickActionTags()
        {
            QuickActionsContainer.Children.Clear();
            var settings = AIService.ReadSettings();
            var tags = settings.QuickActions ?? AIService.GetDefaultQuickActions();

            if (tags.Count == 0) return;

            var titleBlock = new TextBlock
            {
                Text = "Quick Actions:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C084FC")),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            QuickActionsContainer.Children.Add(titleBlock);

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag.Label) || string.IsNullOrWhiteSpace(tag.Prompt))
                    continue;

                var pill = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#120924")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B1F5E")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(0, 0, 5, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = tag.Prompt
                };

                var text = new TextBlock
                {
                    Text = tag.Label,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8B4FE")),
                    FontSize = 11
                };
                pill.Child = text;

                // Smooth hover feedback with purple glow
                pill.MouseEnter += (s, e) =>
                {
                    pill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#251245"));
                    pill.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A855F7"));
                    text.Foreground = Brushes.White;
                };
                pill.MouseLeave += (s, e) =>
                {
                    pill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#120924"));
                    pill.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B1F5E"));
                    text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8B4FE"));
                };

                string promptToSend = tag.Prompt;
                pill.MouseLeftButtonDown += (s, e) =>
                {
                    _ = SubmitFollowUpMessageAsync(promptToSend);
                };

                QuickActionsContainer.Children.Add(pill);
            }
        }

        #endregion

        #region Follow-up Chat Engine

        private void ChatInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Auto pause the timer when user clicks into chat to avoid unexpected auto-close
            PauseTimer();
        }

        private void ChatInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ChatPlaceholder.Visibility = string.IsNullOrEmpty(ChatInputBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChatInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                _ = SubmitFollowUpMessageAsync();
            }
        }

        private void SendButton_Click(object sender, MouseButtonEventArgs e)
        {
            _ = SubmitFollowUpMessageAsync();
        }

        private async System.Threading.Tasks.Task SubmitFollowUpMessageAsync(string? explicitPrompt = null)
        {
            if (_isSending) return;

            string userPrompt = (explicitPrompt ?? ChatInputBox.Text).Trim();
            if (string.IsNullOrWhiteSpace(userPrompt)) return;

            // Pause timer so the conversation stays active
            PauseTimer();

            ChatInputBox.Clear();
            _isSending = true;

            // Update Send button state
            SendBtn.IsEnabled = false;
            SendBtn.Opacity = 0.6;
            SendBtnIcon.Text = "⏳";
            SendBtnText.Text = "Thinking...";

            // Append user prompt to UI and history
            _chatHistory.Add(new AIService.ChatMessage { Role = "user", Content = userPrompt });
            MarkdownHelper.AppendUserMessage(AnswerBox.Document, userPrompt);
            var thinkingBlock = MarkdownHelper.AppendThinkingIndicator(AnswerBox.Document);
            AnswerBox.ScrollToEnd();

            try
            {
                string aiReply = await AIService.GetChatResponseAsync(_chatHistory);
                _chatHistory.Add(new AIService.ChatMessage { Role = "assistant", Content = aiReply });

                MarkdownHelper.RemoveBlock(AnswerBox.Document, thinkingBlock);
                MarkdownHelper.AppendAiResponse(AnswerBox.Document, aiReply);
                AnswerBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                MarkdownHelper.RemoveBlock(AnswerBox.Document, thinkingBlock);
                MarkdownHelper.AppendAiResponse(AnswerBox.Document, $"⚠️ **Error:** {ex.Message}");
                AnswerBox.ScrollToEnd();
            }
            finally
            {
                SendBtn.IsEnabled = true;
                SendBtn.Opacity = 1.0;
                SendBtnIcon.Text = "➤";
                SendBtnText.Text = "Send";
                _isSending = false;
                ChatInputBox.Focus();
            }
        }

        #endregion

        #region Timer & Window Controls

        private void TogglePauseTimer()
        {
            if (_timer == null) return;
            if (!_isPaused)
            {
                PauseTimer();
            }
            else
            {
                ResumeTimer();
            }
        }

        private void PauseTimer()
        {
            if (_timer == null || _isPaused) return;

            _timer.Stop();
            _isPaused = true;
            PauseBtnText.Text = "Resume";
            PauseIcon.Text = "▶ ";
            PauseBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA"));
            PauseBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A855F7"));
            TimerLabel.Text = $"Chat active (Paused)";
            AutoCloseProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C084FC"));
        }

        private void ResumeTimer()
        {
            if (_timer == null || !_isPaused) return;

            _timer.Start();
            _isPaused = false;
            PauseBtnText.Text = "Pause";
            PauseIcon.Text = "⏸ ";
            PauseBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A0D30"));
            PauseBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B1F5E"));
            TimerLabel.Text = $"Auto closing in {(_ticksRemaining / 10) + 1}s";
            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#9333EA"), 0.0));
            grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#4F46E5"), 1.0));
            AutoCloseProgress.Foreground = grad;
        }

        private void PauseButton_Click(object sender, MouseButtonEventArgs e)
        {
            TogglePauseTimer();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If the chat input box has focus, let the user type normally (including spaces)
            if (ChatInputBox.IsFocused)
            {
                if (e.Key == Key.Escape)
                {
                    StartFadeOutAndClose();
                    e.Handled = true;
                }
                return;
            }

            // Global shortcut inside popup: Space or P to Pause/Resume countdown timer
            if (e.Key == Key.Space || e.Key == Key.P)
            {
                TogglePauseTimer();
                e.Handled = true;
            }
            // Ctrl+D to Save to Google Docs
            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.D)
            {
                SaveToDocs_Click(this, null);
                e.Handled = true;
            }
            // Full Screen toggle shortcut: F11 or F
            else if (e.Key == Key.F11 || e.Key == Key.F)
            {
                string currentPos = ReadSavedPosition();
                if (currentPos == "FullScreen")
                {
                    ApplyPosition("TopRight");
                }
                else
                {
                    ApplyPosition("FullScreen");
                }
                e.Handled = true;
            }
            // Escape to dismiss popup
            else if (e.Key == Key.Escape)
            {
                StartFadeOutAndClose();
                e.Handled = true;
            }
        }

        private void StartFadeOutAndClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            _timer?.Stop();

            var fadeAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            fadeAnimation.Completed += (s, args) => Close();
            BeginAnimation(Window.OpacityProperty, fadeAnimation);
        }

        private void SettingsButton_Click(object sender, MouseButtonEventArgs e)
        {
            int originalW = (int)this.Width;
            int originalH = (int)this.Height;
            string currentPos = ReadSavedPosition();

            var settingsWin = new SettingsWindow
            {
                OnSizePreview = (w, h) =>
                {
                    this.Width = w;
                    this.Height = h;
                    this.ApplyPosition(currentPos, w, h);
                }
            };

            settingsWin.ShowDialog();

            if (!settingsWin.IsSaved)
            {
                // Revert to original size if user cancelled or closed without saving
                this.Width = originalW;
                this.Height = originalH;
                ApplyPosition(currentPos, originalW, originalH);
            }
            else
            {
                // Refresh active model badge text, dynamic quick actions, and size
                var settings = AIService.ReadSettings();
                string provider = settings.Provider;
                string modelId = provider switch
                {
                    "OpenRouter" => settings.OpenRouterModel,
                    "Groq" => settings.GroqModel,
                    _ => settings.GeminiModel
                };
                ModelTagText.Text = AIService.GetModelDisplayName(provider, modelId);
                LoadQuickActionTags();
                this.Topmost = settings.AlwaysOnTop;
                UpdatePinVisuals();
                ApplyPosition(ReadSavedPosition());
            }
        }

        private void LoadLogoImage()
        {
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "SMini_.png");
                if (File.Exists(logoPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    AppLogoImage.Source = bitmap;
                }
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = AIService.ReadSettings();
            this.Topmost = settings.AlwaysOnTop;
            UpdatePinVisuals();

            string savedPos = ReadSavedPosition();
            ApplyPosition(savedPos);
            this.Focus();
        }

        private void PinButton_Click(object sender, MouseButtonEventArgs e)
        {
            this.Topmost = !this.Topmost;

            var settings = AIService.ReadSettings();
            settings.AlwaysOnTop = this.Topmost;
            AIService.SaveSettings(settings);

            UpdatePinVisuals();
        }

        private void UpdatePinVisuals()
        {
            if (PinBtn == null || PinIconText == null) return;

            if (this.Topmost)
            {
                PinIconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C084FC"));
                PinBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#251245"));
                PinBtn.ToolTip = "Always on Top: ON (Click to unpin window)";
            }
            else
            {
                PinIconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C4B5FD"));
                PinBtn.Background = Brushes.Transparent;
                PinBtn.ToolTip = "Always on Top: OFF (Click to pin window on top)";
            }
        }

        private string ReadSavedPosition()
        {
            var settings = AIService.ReadSettings();
            return string.IsNullOrWhiteSpace(settings.WindowPosition) ? "TopRight" : settings.WindowPosition;
        }

        private void SavePositionPreference(string positionName)
        {
            try
            {
                var settings = AIService.ReadSettings();
                settings.WindowPosition = positionName;
                AIService.SaveSettings(settings);
            }
            catch { }
        }

        private void ApplyPosition(string positionName, double? customWidth = null, double? customHeight = null)
        {
            var workArea = SystemParameters.WorkArea;
            double padding = 8;

            ResetButtonHighlights();

            if (positionName == "FullScreen")
            {
                Width = workArea.Width - (padding * 2);
                Height = workArea.Height - (padding * 2);
                Left = workArea.Left + padding;
                Top = workArea.Top + padding;
                HighlightButton(BtnFullScreen);
                SavePositionPreference("FullScreen");
                return;
            }

            if (customWidth.HasValue && customWidth.Value >= 400)
            {
                Width = customWidth.Value;
            }
            else
            {
                var settings = AIService.ReadSettings();
                if (settings.WindowWidth >= 400 && settings.WindowWidth < workArea.Width)
                    Width = settings.WindowWidth;
                else
                    Width = 680;
            }

            if (customHeight.HasValue && customHeight.Value >= 300)
            {
                Height = customHeight.Value;
            }
            else
            {
                var settings = AIService.ReadSettings();
                if (settings.WindowHeight >= 300 && settings.WindowHeight < workArea.Height)
                    Height = settings.WindowHeight;
                else
                    Height = 580;
            }

            switch (positionName)
            {
                case "TopLeft":
                    Left = workArea.Left + padding;
                    Top = workArea.Top + padding;
                    HighlightButton(BtnTopLeft);
                    break;

                case "TopRight":
                    Left = workArea.Right - Width + padding;
                    Top = workArea.Top + padding;
                    HighlightButton(BtnTopRight);
                    break;

                case "BottomLeft":
                    Left = workArea.Left + padding;
                    Top = workArea.Bottom - Height + padding;
                    HighlightButton(BtnBottomLeft);
                    break;

                case "BottomRight":
                    Left = workArea.Right - Width + padding;
                    Top = workArea.Bottom - Height + padding;
                    HighlightButton(BtnBottomRight);
                    break;

                case "Center":
                default:
                    Left = workArea.Left + (workArea.Width - Width) / 2;
                    Top = workArea.Top + (workArea.Height - Height) / 2;
                    HighlightButton(BtnCenter);
                    break;
            }

            SavePositionPreference(positionName);
        }

        private void HighlightButton(System.Windows.Controls.Border btn)
        {
            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8D3BF0"), 0.0));
            grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0B33D3"), 1.0));
            btn.Background = grad;
            if (btn.Child is TextBlock tb)
            {
                tb.Foreground = Brushes.White;
                tb.FontWeight = FontWeights.Bold;
            }
        }

        private void ResetButtonHighlights()
        {
            var transparent = Brushes.Transparent;
            var muted = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));

            void ResetBtn(System.Windows.Controls.Border btn)
            {
                btn.Background = transparent;
                if (btn.Child is TextBlock tb)
                {
                    tb.Foreground = muted;
                    tb.FontWeight = FontWeights.Normal;
                }
            }

            ResetBtn(BtnTopLeft);
            ResetBtn(BtnTopRight);
            ResetBtn(BtnCenter);
            ResetBtn(BtnBottomLeft);
            ResetBtn(BtnBottomRight);
            ResetBtn(BtnFullScreen);
        }

        private void SetPos_TopLeft(object sender, MouseButtonEventArgs e) => ApplyPosition("TopLeft");
        private void SetPos_TopRight(object sender, MouseButtonEventArgs e) => ApplyPosition("TopRight");
        private void SetPos_Center(object sender, MouseButtonEventArgs e) => ApplyPosition("Center");
        private void SetPos_BottomLeft(object sender, MouseButtonEventArgs e) => ApplyPosition("BottomLeft");
        private void SetPos_BottomRight(object sender, MouseButtonEventArgs e) => ApplyPosition("BottomRight");
        private void SetPos_FullScreen(object sender, MouseButtonEventArgs e)
        {
            string currentPos = ReadSavedPosition();
            if (currentPos == "FullScreen")
            {
                ApplyPosition("TopRight");
            }
            else
            {
                ApplyPosition("FullScreen");
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CopyButton_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string textToCopy;
                if (!string.IsNullOrWhiteSpace(AnswerBox.Selection.Text))
                {
                    textToCopy = AnswerBox.Selection.Text;
                }
                else
                {
                    var lastAssistant = _chatHistory.FindLast(m => m.Role == "assistant");
                    textToCopy = lastAssistant?.Content ?? _initialAnswerText;
                }

                Clipboard.SetText(textToCopy);
                CopyBtnText.Text = "Copied!";
                CopyIcon.Text = "✓ ";

                var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                resetTimer.Tick += (s, args) =>
                {
                    resetTimer.Stop();
                    CopyBtnText.Text = "Copy";
                    CopyIcon.Text = "📋 ";
                };
                resetTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy: {ex.Message}", "SnapMini");
            }
        }

        private async void SaveToDocs_Click(object sender, MouseButtonEventArgs? e)
        {
            try
            {
                PauseTimer();
                SaveDocBtnText.Text = "Saving...";
                SaveDocIcon.Text = "⏳ ";

                string questionText = QuestionBox.Text;
                var lastAssistant = _chatHistory.FindLast(m => m.Role == "assistant");
                string answerText = lastAssistant?.Content ?? _initialAnswerText;

                AIService.GoogleExportTarget? selectedTarget = null;
                if (GoogleTargetComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is AIService.GoogleExportTarget t)
                {
                    selectedTarget = t;
                }

                string target = await GoogleDocsService.ExportQaSummaryAsync(questionText, answerText, selectedTarget);

                if (selectedTarget != null)
                {
                    try
                    {
                        var settings = AIService.ReadSettings();
                        settings.LastSelectedGoogleTargetId = selectedTarget.Id;
                        AIService.SaveSettings(settings);
                    }
                    catch { }
                }

                string destName = selectedTarget != null && !string.IsNullOrWhiteSpace(selectedTarget.Name)
                    ? selectedTarget.Name
                    : "Google Docs";

                if (!string.IsNullOrWhiteSpace(target) && target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try { Clipboard.SetText(target); } catch { }
                    SaveDocBtn.ToolTip = $"Saved to {destName}!\nLink copied to clipboard:\n{target}";
                }

                SaveDocBtnText.Text = "Saved!";
                SaveDocIcon.Text = "✓ ";

                var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                resetTimer.Tick += (s, args) =>
                {
                    resetTimer.Stop();
                    SaveDocBtnText.Text = "Save to Docs";
                    SaveDocIcon.Text = "📄 ";
                    UpdateSaveDocButtonToolTip();
                };
                resetTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save to Google Docs:\n{ex.Message}", "SnapMini Google Export");
                SaveDocBtnText.Text = "Save to Docs";
                SaveDocIcon.Text = "📄 ";
                UpdateSaveDocButtonToolTip();
            }
        }

        private void ExpandContextBtn_Click(object sender, MouseButtonEventArgs e)
        {
            _isContextExpanded = !_isContextExpanded;
            if (_isContextExpanded)
            {
                QuestionBox.MaxHeight = double.PositiveInfinity;
                ExpandContextIcon.Text = "▲";
                ExpandContextIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9D5FF"));
                ExpandContextBtn.ToolTip = "Collapse Context";
            }
            else
            {
                QuestionBox.MaxHeight = 90;
                ExpandContextIcon.Text = "▼";
                ExpandContextIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C084FC"));
                ExpandContextBtn.ToolTip = "Expand Context";
            }
        }

        private void CloseButton_Click(object sender, MouseButtonEventArgs e)
        {
            StartFadeOutAndClose();
        }

        #endregion
    }
}
