using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SnapMini
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

        private const int WM_HOTKEY = 0x0312;

        private const int HOTKEY_TEXT_ID = 9001;
        private const int HOTKEY_SCREENSHOT_ID = 9002;

        // Virtual Key codes: 'A' = 0x41, 'S' = 0x53
        private const uint VK_A = 0x41;
        private const uint VK_S = 0x53;

        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;

        /// <summary>
        /// Registers global hotkeys (Ctrl + Alt + A & Ctrl + Alt + S).
        /// </summary>
        public void Register(Window invisibleMessageWindow)
        {
            var helper = new WindowInteropHelper(invisibleMessageWindow);
            _windowHandle = helper.Handle;

            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(HwndHook);

            // Hotkey 1: Ctrl + Alt + A (Ask / Selected Text QA)
            bool textHotkeySuccess = RegisterHotKey(_windowHandle, HOTKEY_TEXT_ID, MOD_CONTROL | MOD_ALT, VK_A);
            
            // Hotkey 2: Ctrl + Alt + S (Screenshot Snipping QA)
            bool screenshotHotkeySuccess = RegisterHotKey(_windowHandle, HOTKEY_SCREENSHOT_ID, MOD_CONTROL | MOD_ALT, VK_S);

            if (!textHotkeySuccess || !screenshotHotkeySuccess)
            {
                System.Windows.MessageBox.Show(
                    "Warning: Could not register hotkeys (Ctrl+Alt+A / Ctrl+Alt+S). Another app may be using them.",
                    "SnapMini");
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();

                if (hotkeyId == HOTKEY_TEXT_ID)
                {
                    _ = HandleSelectedTextHotkeyAsync();
                    handled = true;
                }
                else if (hotkeyId == HOTKEY_SCREENSHOT_ID)
                {
                    _ = HandleScreenshotHotkeyAsync();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Triggered by Ctrl + Alt + A.
        /// Programmatically releases Alt key state and sends Ctrl+C to copy selected browser text.
        /// </summary>
        private async Task HandleSelectedTextHotkeyAsync()
        {
            try
            {
                // 1. Clear clipboard first so stale/old clipboard text is never reused
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { System.Windows.Clipboard.Clear(); } catch { }
                });

                // 2. Short pause for user to release shortcut keys
                await Task.Delay(100);

                // 3. Programmatically release Alt & Ctrl keys and send clean Ctrl+C
                SimulateCleanCopy();

                // 4. Poll clipboard for up to 1 second to capture newly copied text
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
                        "No highlighted text detected. Highlight text and try pressing Ctrl+Alt+A (or press Ctrl+C first).",
                        "SnapMini");
                    return;
                }

                // 5. Query Gemini API with selected text
                string answer = await GeminiService.GetAnswerAsync(selectedText);

                // 6. Show Answer Window
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var win = new AnswerWindow(selectedText, answer);
                    win.Show();
                    win.Activate();
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error processing selected text:\n{ex.Message}", "SnapMini Error");
            }
        }

        /// <summary>
        /// Triggered by Ctrl + Alt + S.
        /// Opens Windows Snipping Tool automatically, waits for user to snip screen area, reads image via WinForms+WPF fallback, runs OCR & Gemini.
        /// </summary>
        private async Task HandleScreenshotHotkeyAsync()
        {
            try
            {
                // Clear existing clipboard image first
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { System.Windows.Clipboard.Clear(); } catch { }
                });

                // Launch Windows Snipping Tool overlay directly
                try
                {
                    Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
                }
                catch
                {
                    // Fallback if protocol unavailable
                }

                // Poll for up to 15 seconds for user to complete screenshot snip
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
                    // User canceled snip or timed out
                    return;
                }

                // Run native OCR on snippet image
                string ocrText = await OcrService.ExtractTextAsync(image);

                if (string.IsNullOrWhiteSpace(ocrText))
                {
                    System.Windows.MessageBox.Show("OCR could not detect any text in the screenshot.", "SnapMini");
                    return;
                }

                // Ask Gemini API
                string answer = await GeminiService.GetAnswerAsync(ocrText);

                // Display answer popup
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var win = new AnswerWindow(ocrText, answer);
                    win.Show();
                    win.Activate();
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error processing screenshot:\n{ex.Message}", "SnapMini Error");
            }
        }

        /// <summary>
        /// Robust clipboard image reader supporting both WinForms DIB formats (used by Snipping Tool) and WPF formats.
        /// </summary>
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
                // Clipboard locked by OS temporarily
            }

            return null;
        }

        /// <summary>
        /// Converts GDI System.Drawing.Image to WPF BitmapSource.
        /// </summary>
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

        /// <summary>
        /// Programmatically releases Alt, Ctrl, and Shift modifier keys before sending Ctrl+C.
        /// Fixes Windows turning Ctrl+C into Ctrl+Alt+C while user holds hotkey.
        /// </summary>
        private static void SimulateCleanCopy()
        {
            // Force release Alt, Ctrl, Shift keys
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // Send Ctrl DOWN -> C DOWN -> C UP -> Ctrl UP
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void Dispose()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_TEXT_ID);
                UnregisterHotKey(_windowHandle, HOTKEY_SCREENSHOT_ID);
            }
            _hwndSource?.RemoveHook(HwndHook);
        }
    }
}
