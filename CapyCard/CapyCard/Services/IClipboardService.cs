using System.IO;
using System.Threading.Tasks;

namespace CapyCard.Services
{
    public interface IClipboardService
    {
        Task<Stream?> GetImageAsync();
        Task<bool> HasImageAsync();
        Task SetTextAsync(string text);
    }

    public static class ClipboardService
    {
        public static IClipboardService? Current { get; set; }
    }
}
