using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SnapMini
{
    /// <summary>
    /// Service responsible for extracting text from images using native Windows OCR API.
    /// Uses language packs installed in Windows Settings.
    /// </summary>
    public static class OcrService
    {
        /// <summary>
        /// Asynchronously extracts text from a WPF BitmapSource image.
        /// </summary>
        /// <param name="image">The image captured from the clipboard.</param>
        /// <returns>Extracted text string from OCR.</returns>
        public static async Task<string> ExtractTextAsync(BitmapSource image)
        {
            // Convert WPF BitmapSource to Windows SoftwareBitmap expected by Windows.Media.Ocr
            SoftwareBitmap softwareBitmap = await ToSoftwareBitmapAsync(image);

            // Attempt to initialize OCR engine with English first
            var preferredLanguage = new Language("en");
            OcrEngine? engine = OcrEngine.IsLanguageSupported(preferredLanguage)
                ? OcrEngine.TryCreateFromLanguage(preferredLanguage)
                : null;

            // Fallback to user's primary system language if English pack isn't present
            engine ??= OcrEngine.TryCreateFromUserProfileLanguages();

            if (engine == null)
            {
                throw new InvalidOperationException(
                    "No Windows OCR language pack found. Please install a language with OCR enabled in " +
                    "Windows Settings -> Time & Language -> Language & region.");
            }

            // Perform recognition on the software bitmap
            OcrResult result = await engine.RecognizeAsync(softwareBitmap);
            return result.Text;
        }

        /// <summary>
        /// Helper function to convert WPF BitmapSource into WinRT SoftwareBitmap.
        /// Explicit namespace qualifiers avoid ambiguity between System.Windows.Media.Imaging and Windows.Graphics.Imaging.
        /// </summary>
        private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(BitmapSource bitmapSource)
        {
            // Save WPF BitmapSource into memory stream as PNG bytes
            using var memoryStream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
            encoder.Save(memoryStream);
            byte[] pngBytes = memoryStream.ToArray();

            // Write PNG bytes into WinRT InMemoryRandomAccessStream
            var randomAccessStream = new InMemoryRandomAccessStream();
            using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
            {
                await outputStream.WriteAsync(pngBytes.AsBuffer());
                await outputStream.FlushAsync();
            }
            randomAccessStream.Seek(0);

            // Decode image stream into WinRT SoftwareBitmap
            Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
    }
}
