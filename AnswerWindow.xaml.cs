using System;
using System.Windows;
using System.Windows.Threading;

namespace SnapMini
{
    /// <summary>
    /// Code-behind for AnswerWindow displaying extracted text and Gemini answer.
    /// Includes auto-dismiss timer.
    /// </summary>
    public partial class AnswerWindow : Window
    {
        public AnswerWindow(string questionText, string answerText)
        {
            InitializeComponent();

            // Set content text
            QuestionBox.Text = questionText.Trim();
            AnswerText.Text = answerText.Trim();

            // Auto-close window after 25 seconds of display
            var autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(25)
            };
            autoCloseTimer.Tick += (sender, e) =>
            {
                autoCloseTimer.Stop();
                Close();
            };
            autoCloseTimer.Start();
        }
    }
}