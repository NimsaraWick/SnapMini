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
    /// screen position picker (Top-Left, Top-Right, Center, Bottom-Left, Bottom-Right),
    /// displays Images/SM_logo.png in the header bar, configurable auto-close timer with smooth fade-out,
    /// and quick action suggestion pills.
    /// </summary>
    public partial class AnswerWindow : Window
    {
        private readonly DispatcherTimer? _timer;
        private int _ticksRemaining = 250;
        private bool _isPaused = false;
        private bool _isClosing = false;
        private bool _isSending = false;
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

        private void QuickPrompt_Simpler(object sender, MouseButtonEventArgs e) => _ = SubmitFollowUpMessageAsync("Could you explain this in simpler terms with a clear summary?");
        private void QuickPrompt_Examples(object sender, MouseButtonEventArgs e) => _ = SubmitFollowUpMessageAsync("Can you provide a couple of practical real-world examples illustrating this?");
        private void QuickPrompt_Grammar(object sender, MouseButtonEventArgs e) => _ = SubmitFollowUpMessageAsync("Please break down the sentence structure and key grammar/vocabulary used here.");
        private void QuickPrompt_BulletPoints(object sender, MouseButtonEventArgs e) => _ = SubmitFollowUpMessageAsync("Summarize the key takeaways into concise bullet points.");

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

        private void PauseTimer()
        {
            if (_timer == null || _isPaused) return;

            _timer.Stop();
            _isPaused = true;
            PauseBtnText.Text = "Resume";
            PauseIcon.Text = "▶ ";
            PauseBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
            TimerLabel.Text = $"Chat active (Paused)";
            AutoCloseProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
        }

        private void ResumeTimer()
        {
            if (_timer == null || !_isPaused) return;

            _timer.Start();
            _isPaused = false;
            PauseBtnText.Text = "Pause";
            PauseIcon.Text = "⏸ ";
            PauseBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            TimerLabel.Text = $"Auto closing in {(_ticksRemaining / 10) + 1}s";
            AutoCloseProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
        }

        private void PauseButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_isPaused)
            {
                PauseTimer();
            }
            else
            {
                ResumeTimer();
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
            var settingsWin = new SettingsWindow();
            settingsWin.ShowDialog();

            // Refresh active model badge text
            var settings = AIService.ReadSettings();
            string provider = settings.Provider;
            string modelId = provider switch
            {
                "OpenRouter" => settings.OpenRouterModel,
                "Groq" => settings.GroqModel,
                _ => settings.GeminiModel
            };
            ModelTagText.Text = AIService.GetModelDisplayName(provider, modelId);
        }

        private void LoadLogoImage()
        {
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "SM_logo.png");
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
            string savedPos = ReadSavedPosition();
            ApplyPosition(savedPos);
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

        private void ApplyPosition(string positionName)
        {
            var workArea = SystemParameters.WorkArea;
            double padding = 8;

            ResetButtonHighlights();

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
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
        }

        private void ResetButtonHighlights()
        {
            var transparent = Brushes.Transparent;
            BtnTopLeft.Background = transparent;
            BtnTopRight.Background = transparent;
            BtnCenter.Background = transparent;
            BtnBottomLeft.Background = transparent;
            BtnBottomRight.Background = transparent;
        }

        private void SetPos_TopLeft(object sender, MouseButtonEventArgs e) => ApplyPosition("TopLeft");
        private void SetPos_TopRight(object sender, MouseButtonEventArgs e) => ApplyPosition("TopRight");
        private void SetPos_Center(object sender, MouseButtonEventArgs e) => ApplyPosition("Center");
        private void SetPos_BottomLeft(object sender, MouseButtonEventArgs e) => ApplyPosition("BottomLeft");
        private void SetPos_BottomRight(object sender, MouseButtonEventArgs e) => ApplyPosition("BottomRight");

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
                    // Copy the latest assistant response or all conversation if available
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

        private void CloseButton_Click(object sender, MouseButtonEventArgs e)
        {
            StartFadeOutAndClose();
        }

        #endregion
    }
}
