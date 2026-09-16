using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SnapMini.Services;
using SnapMini.Views;

namespace SnapMini.Helpers
{
    /// <summary>
    /// Manages native Windows global hotkeys (RegisterHotKey API).
    /// Listens for shortcut combinations system-wide across all applications including web browsers.
    /// </summary>
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12; // Alt key
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;

        private const int WM_HOTKEY = 0x0312;

        private const int HOTKEY_TEXT_ALT_ID = 9001;
        private const int HOTKEY_SCREENSHOT_ALT_ID = 9002;
        private const int HOTKEY_TEXT_SHIFT_ID = 9003;
        private const int HOTKEY_SCREENSHOT_SHIFT_ID = 9004;

        private const uint VK_A = 0x41;
        private const uint VK_S = 0x53;

        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;

        public void Register(Window invisibleMessageWindow)
        {
            var helper = new WindowInteropHelper(invisibleMessageWindow);
            _windowHandle = helper.Handle;

            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(HwndHook);

            bool textAltSuccess = RegisterHotKey(_windowHandle, HOTKEY_TEXT_ALT_ID, MOD_CONTROL | MOD_ALT, VK_A);
            bool textShiftSuccess = RegisterHotKey(_windowHandle, HOTKEY_TEXT_SHIFT_ID, MOD_CONTROL | MOD_SHIFT, VK_A);

            bool ssAltSuccess = RegisterHotKey(_windowHandle, HOTKEY_SCREENSHOT_ALT_ID, MOD_CONTROL | MOD_ALT, VK_S);
            bool ssShiftSuccess = RegisterHotKey(_windowHandle, HOTKEY_SCREENSHOT_SHIFT_ID, MOD_CONTROL | MOD_SHIFT, VK_S);

            var failedText = new System.Collections.Generic.List<string>();
            if (!textAltSuccess) failedText.Add("Ctrl+Alt+A");
            if (!textShiftSuccess) failedText.Add("Ctrl+Shift+A");

            if (!textAltSuccess && !textShiftSuccess)
            {
                System.Windows.MessageBox.Show(
                    "Warning: Could not register text hotkeys (Ctrl+Shift+A / Ctrl+Alt+A). Another app (e.g. QQ, WeChat, PowerToys, Snagit) is using them.",
                    "SnapMini");
            }
            else if (failedText.Count > 0)
            {
                Debug.WriteLine($"SnapMini: Failed to register hotkey(s): {string.Join(", ", failedText)}. (Another app may be using them)");
            }

            var failedSs = new System.Collections.Generic.List<string>();
            if (!ssAltSuccess) failedSs.Add("Ctrl+Alt+S");
            if (!ssShiftSuccess) failedSs.Add("Ctrl+Shift+S");

            if (!ssAltSuccess && !ssShiftSuccess)
            {
                System.Windows.MessageBox.Show(
                    "Warning: Could not register screenshot hotkeys (Ctrl+Shift+S / Ctrl+Alt+S). Another app is using them.",
                    "SnapMini");
            }
            else if (failedSs.Count > 0)
            {
                Debug.WriteLine($"SnapMini: Failed to register screenshot hotkey(s): {string.Join(", ", failedSs)}.");
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();

                if (hotkeyId == HOTKEY_TEXT_ALT_ID || hotkeyId == HOTKEY_TEXT_SHIFT_ID)
                {
                    _ = HandleSelectedTextHotkeyAsync();
                    handled = true;
                }
                else if (hotkeyId == HOTKEY_SCREENSHOT_ALT_ID || hotkeyId == HOTKEY_SCREENSHOT_SHIFT_ID)
                {
                    _ = HandleScreenshotHotkeyAsync();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private async Task HandleSelectedTextHotkeyAsync()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { System.Windows.Clipboard.Clear(); } catch { }
                });

                await Task.Delay(100);
                SimulateCleanCopy();

                string selectedText = string.Empty;
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(100);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            selectedText = System.Windows.Clipboard.GetText();
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(selectedText))
                        break;
                }

                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    System.Windows.MessageBox.Show(
                        "No highlighted text detected. Highlight text and try pressing Ctrl+Shift+A or Ctrl+Alt+A (or press Ctrl+C first).",
                        "SnapMini");
                    return;
                }

                string answer = await AIService.GetAnswerAsync(selectedText);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var win = new AnswerWindow(selectedText, answer, AIService.CurrentModelDisplayName);
                    win.Show();
                    win.Activate();
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error processing selected text:\n{ex.Message}", "SnapMini Error");
            }
        }

        private async Task HandleScreenshotHotkeyAsync()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { System.Windows.Clipboard.Clear(); } catch { }
                });

                try
                {
                    Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
                }
                catch
                {
                }

                BitmapSource? image = null;
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(250);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        image = GetImageFromClipboard();
                    });

                    if (image != null)
                        break;
                }

                if (image == null)
                {
                    return;
                }

                string ocrText = await OcrService.ExtractTextAsync(image);

                if (string.IsNullOrWhiteSpace(ocrText))
                {
                    System.Windows.MessageBox.Show("OCR could not detect any text in the screenshot.", "SnapMini");
                    return;
                }

                string answer = await AIService.GetAnswerAsync(ocrText);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var win = new AnswerWindow(ocrText, answer, AIService.CurrentModelDisplayName);
                    win.Show();
                    win.Activate();
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error processing screenshot:\n{ex.Message}", "SnapMini Error");
            }
        }

        private static BitmapSource? GetImageFromClipboard()
        {
            try
            {
                if (System.Windows.Forms.Clipboard.ContainsImage())
                {
                    var drawImage = System.Windows.Forms.Clipboard.GetImage();
                    if (drawImage != null)
                    {
                        return ConvertToBitmapSource(drawImage);
                    }
                }

                if (System.Windows.Clipboard.ContainsImage())
                {
                    return System.Windows.Clipboard.GetImage();
                }
            }
            catch
            {
            }

            return null;
        }

        private static BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            using var bitmap = new System.Drawing.Bitmap(image);
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private static void SimulateCleanCopy()
        {
            keybd_event((byte)VK_A, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_S, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void Dispose()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_TEXT_ALT_ID);
                UnregisterHotKey(_windowHandle, HOTKEY_TEXT_SHIFT_ID);
                UnregisterHotKey(_windowHandle, HOTKEY_SCREENSHOT_ALT_ID);
                UnregisterHotKey(_windowHandle, HOTKEY_SCREENSHOT_SHIFT_ID);
            }
            _hwndSource?.RemoveHook(HwndHook);
        }
    }
}
