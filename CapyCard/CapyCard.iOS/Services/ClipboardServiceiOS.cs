using System;
using System.IO;
using System.Threading.Tasks;
using CapyCard.Services;
using Foundation;
using UIKit;

namespace CapyCard.iOS.Services
{
    public class ClipboardServiceiOS : IClipboardService
    {
        public Task<bool> HasImageAsync()
        {
            return Task.FromResult(UIPasteboard.General.HasImages);
        }

        public Task<Stream?> GetImageAsync()
        {
            try
            {
                var image = UIPasteboard.General.Image;
                if (image != null)
                {
                    var data = image.AsJPEG(0.8f);
                    if (data != null)
                    {
                        var stream = new MemoryStream();
                        data.AsStream().CopyTo(stream);
                        stream.Position = 0;
                        return Task.FromResult<Stream?>(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClipboardiOS] Error: {ex}");
            }
            return Task.FromResult<Stream?>(null);
        }

        public Task SetTextAsync(string text)
        {
            try
            {
                UIPasteboard.General.String = text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClipboardiOS] SetTextAsync Error: {ex}");
            }
            return Task.CompletedTask;
        }
    }
}
