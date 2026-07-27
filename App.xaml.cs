using System.Drawing;
using System.Windows;
using System.Windows.Forms;

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

            // Prevent app from closing when popup windows close
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize System Tray Icon with shortcut instructions in tooltip
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "SnapMini (Gemini)\n• Ctrl+Alt+A: Selected Text\n• Ctrl+Alt+S: Screenshot"
            };

            // Add right-click context menu to Exit
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (sender, args) => Shutdown());
            _trayIcon.ContextMenuStrip = contextMenu;

            // Create an invisible window required to attach Win32 Hotkey Hook
            _hiddenWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            // Register global hotkeys
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.Register(_hiddenWindow);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up registered hotkeys, tray icon, and hidden window
            _hotkeyManager?.Dispose();
            _trayIcon?.Dispose();
            _hiddenWindow?.Close();

            base.OnExit(e);
        }
    }
}