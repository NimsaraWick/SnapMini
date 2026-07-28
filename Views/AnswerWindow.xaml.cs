using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SnapMini.Services;

namespace SnapMini.Views
{
    /// <summary>
    /// Code-behind for AnswerWindow. Includes screen position picker (Top-Left, Top-Right, Center, Bottom-Left, Bottom-Right)
    /// and saves user position preference for future popups.
    /// </summary>
    public partial class AnswerWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private int _ticksRemaining = 250;
        private static readonly string PositionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_position.txt");

        public AnswerWindow(string questionText, string answerText, string modelName = "")
        {
            InitializeComponent();

            QuestionBox.Text = questionText.Trim();
            AnswerText.Text = answerText.Trim();
            ModelTagText.Text = string.IsNullOrWhiteSpace(modelName) ? AIService.CurrentModelDisplayName : modelName;

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
                    Close();
                }
            };
            _timer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string savedPos = ReadSavedPosition();
            ApplyPosition(savedPos);
        }

        private string ReadSavedPosition()
        {
            if (File.Exists(PositionFilePath))
            {
                try
                {
                    string pos = File.ReadAllText(PositionFilePath).Trim();
                    if (!string.IsNullOrEmpty(pos)) return pos;
                }
                catch { }
            }
            return "TopRight";
        }

        private void SavePositionPreference(string positionName)
        {
            try
            {
                File.WriteAllText(PositionFilePath, positionName);
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
            _timer.Stop();
            Close();
        }
    }
}
