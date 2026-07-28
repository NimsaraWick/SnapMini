using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using SnapMini.Helpers;

namespace SnapMini
{
    /// <summary>
    /// Application entry point configuring System Tray icon and registering Global Hotkeys.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _trayIcon;
        private HotkeyManager? _hotkeyManager;
        private Window? _hiddenWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "SnapMini (AI Assistant)\n• Ctrl+Alt+A: Selected Text\n• Ctrl+Alt+S: Screenshot"
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (sender, args) => Shutdown());
            _trayIcon.ContextMenuStrip = contextMenu;

            _hiddenWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.Register(_hiddenWindow);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyManager?.Dispose();
            _trayIcon?.Dispose();
            _hiddenWindow?.Close();

            base.OnExit(e);
        }
    }
}