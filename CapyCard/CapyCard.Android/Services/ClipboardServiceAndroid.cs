using System;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Avalonia.Android;
using CapyCard.Services;
using Xamarin.Essentials;

namespace CapyCard.Android.Services
{
    public class ClipboardServiceAndroid : IClipboardService
    {
        public Task<bool> HasImageAsync()
        {
            try
            {
                // Fallback to native ClipboardManager if Essentials fails
                var clipboardManager = (ClipboardManager?)Platform.AppContext.GetSystemService(Context.ClipboardService);
                if (clipboardManager?.PrimaryClipDescription != null)
                {
                    var desc = clipboardManager.PrimaryClipDescription;
                    System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] HasImageAsync checking. MimeCount: {desc.MimeTypeCount}");
                    for (int i = 0; i < desc.MimeTypeCount; i++)
                    {
                        var mime = desc.GetMimeType(i);
                        System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] Found MIME: {mime}");
                    }

                    if (desc.HasMimeType("image/*"))
                    {
                        System.Diagnostics.Debug.WriteLine("[ClipboardAndroid] HasMimeType('image/*') is true.");
                        return Task.FromResult(true);
                    }
                }
                else 
                {
                    System.Diagnostics.Debug.WriteLine("[ClipboardAndroid] ClipboardManager or PrimaryClipDescription is null.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] HasImageAsync Error: {ex}");
            }
            return Task.FromResult(false);
        }

        public async Task<Stream?> GetImageAsync()
        {
            try
            {
                var clipboardManager = (ClipboardManager?)Platform.AppContext.GetSystemService(Context.ClipboardService);
                if (clipboardManager == null || !clipboardManager.HasPrimaryClip)
                {
                    System.Diagnostics.Debug.WriteLine("[ClipboardAndroid] GetImageAsync: No Primary Clip.");
                    return null;
                }

                var item = clipboardManager.PrimaryClip?.GetItemAt(0);
                if (item == null || item.Uri == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] GetImageAsync: Item or URI is null. Text: {item?.Text}");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] GetImageAsync: Processing URI: {item.Uri}");

                var context = Platform.AppContext;
                var stream = context.ContentResolver?.OpenInputStream(item.Uri);
                
                if (stream != null)
                {
                    System.Diagnostics.Debug.WriteLine("[ClipboardAndroid] GetImageAsync: Stream opened successfully.");
                    var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    stream.Close();
                    ms.Position = 0;
                    System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] GetImageAsync: Copied {ms.Length} bytes.");
                    return ms;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ClipboardAndroid] GetImageAsync: Failed to open stream.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] GetImageAsync Error: {ex}");
            }
            return null;
        }

        public async Task SetTextAsync(string text)
        {
            try
            {
                await Xamarin.Essentials.Clipboard.SetTextAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClipboardAndroid] SetTextAsync Error: {ex}");
            }
        }
    }
}
