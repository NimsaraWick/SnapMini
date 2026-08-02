using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapMini.Services;

namespace SnapMini.Views
{
    /// <summary>
    /// Code-behind for AnswerWindow. Includes screen position picker (Top-Left, Top-Right, Center, Bottom-Left, Bottom-Right),
    /// displays Images/SM_logo.png in the header bar, configurable auto-close timer with smooth fade-out animation, and Settings access.
    /// Position state and timer duration are persisted directly in appsettings.json.
    /// </summary>
    public partial class AnswerWindow : Window
    {
        private readonly DispatcherTimer? _timer;
        private int _ticksRemaining = 250;
        private bool _isPaused = false;
        private bool _isClosing = false;

        public AnswerWindow(string questionText, string answerText, string modelName = "")
        {
            InitializeComponent();

            QuestionBox.Text = questionText.Trim();
            AnswerText.Text = answerText.Trim();
            ModelTagText.Text = string.IsNullOrWhiteSpace(modelName) ? AIService.CurrentModelDisplayName : modelName;

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

        private void PauseButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (_timer == null) return;

            if (!_isPaused)
            {
                // Pause timer
                _timer.Stop();
                _isPaused = true;
                PauseBtnText.Text = "Resume";
                PauseIcon.Text = "▶ ";
                PauseBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
                TimerLabel.Text = $"Paused ({(_ticksRemaining / 10) + 1}s left)";
                AutoCloseProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            }
            else
            {
                // Resume timer
                _timer.Start();
                _isPaused = false;
                PauseBtnText.Text = "Pause";
                PauseIcon.Text = "⏸ ";
                PauseBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
                TimerLabel.Text = $"Auto closing in {(_ticksRemaining / 10) + 1}s";
                AutoCloseProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
            }
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
                Clipboard.SetText(AnswerText.Text);
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
    }
}
