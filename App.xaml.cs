using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using SnapMini.Helpers;
using SnapMini.Views;

namespace SnapMini
{
    /// <summary>
    /// Application entry point configuring System Tray icon and registering Global Hotkeys.
    /// Uses Images/SMini.png as custom system tray icon and provides Settings menu.
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

            // Load custom System Tray icon from Images/SMini.png
            Icon trayIconImage = SystemIcons.Application;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "SMini_.png");

            if (File.Exists(iconPath))
            {
                try
                {
                    using var bmp = new Bitmap(iconPath);
                    IntPtr hIcon = bmp.GetHicon();
                    trayIconImage = Icon.FromHandle(hIcon);
                }
                catch
                {
                    trayIconImage = SystemIcons.Application;
                }
            }

            _trayIcon = new NotifyIcon
            {
                Icon = trayIconImage,
                Visible = true,
                Text = "SnapMini (AI Assistant)\n• Ctrl+Alt+A: Selected Text\n• Ctrl+Alt+S: Screenshot"
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, (sender, args) =>
            {
                var settingsWin = new SettingsWindow();
                settingsWin.Show();
                settingsWin.Activate();
            });
            contextMenu.Items.Add("-"); // Separator
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