using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace CapyCard.Services
{
    /// <summary>
    /// Clipboard-Implementierung für Desktop (Windows, macOS, Linux) und Browser.
    /// Nutzt die Avalonia Clipboard API.
    /// </summary>
    public class DesktopClipboardService : IClipboardService
    {
        private object? GetClipboard()
        {
            // Fallback to Lifetime MainWindow
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                return TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
            }
            if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView && singleView.MainView != null)
            {
                return TopLevel.GetTopLevel(singleView.MainView)?.Clipboard;
            }
            return null;
        }

        public Task<Stream?> GetImageAsync()
        {
            return Task.FromResult<Stream?>(null);
        }

        public async Task<bool> HasImageAsync()
        {
            var clipboard = GetClipboard();
            if (clipboard == null) return false;

            try
            {
                var method = clipboard.GetType().GetMethod("GetFormatsAsync");
                if (method != null)
                {
                    var task = (Task<string[]>)method.Invoke(clipboard, null)!;
                    var formats = await task;
                    return formats.Any(f => f.Contains("image", StringComparison.OrdinalIgnoreCase) || 
                                           f.Equals("png", StringComparison.OrdinalIgnoreCase) || 
                                           f.Equals("bitmap", StringComparison.OrdinalIgnoreCase));
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task SetTextAsync(string text)
        {
            var clipboard = GetClipboard();
            bool success = false;

            if (clipboard != null)
            {
                try
                {
                    var method = clipboard.GetType().GetMethod("SetTextAsync", new[] { typeof(string) });
                    if (method != null)
                    {
                        var task = (Task)method.Invoke(clipboard, new object[] { text })!;
                        await task;
                        success = true;
                    }
                }
                catch
                {
                    // Ignore Avalonia errors and try fallback
                }
            }

            // Fallback for macOS: use pbcopy
            if (!success && OperatingSystem.IsMacOS())
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "pbcopy",
                        UseShellExecute = false,
                        RedirectStandardInput = true
                    };
                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process != null)
                    {
                        using (var sw = process.StandardInput)
                        {
                            await sw.WriteAsync(text);
                        }
                        await process.WaitForExitAsync();
                    }
                }
                catch
                {
                    // Ignore fallback errors
                }
            }
        }
    }
}
