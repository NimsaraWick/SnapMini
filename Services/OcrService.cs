using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SnapMini.Services
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
        public static async Task<string> ExtractTextAsync(BitmapSource image)
        {
            SoftwareBitmap softwareBitmap = await ToSoftwareBitmapAsync(image);

            var preferredLanguage = new Language("en");
            OcrEngine? engine = OcrEngine.IsLanguageSupported(preferredLanguage)
                ? OcrEngine.TryCreateFromLanguage(preferredLanguage)
                : null;

            engine ??= OcrEngine.TryCreateFromUserProfileLanguages();

            if (engine == null)
            {
                throw new InvalidOperationException(
                    "No Windows OCR language pack found. Please install a language with OCR enabled in " +
                    "Windows Settings -> Time & Language -> Language & region.");
            }

            OcrResult result = await engine.RecognizeAsync(softwareBitmap);
            return result.Text;
        }

        private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(BitmapSource bitmapSource)
        {
            using var memoryStream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
            encoder.Save(memoryStream);
            byte[] pngBytes = memoryStream.ToArray();

            var randomAccessStream = new InMemoryRandomAccessStream();
            using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
            {
                await outputStream.WriteAsync(pngBytes.AsBuffer());
                await outputStream.FlushAsync();
            }
            randomAccessStream.Seek(0);

            Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
    }
}
