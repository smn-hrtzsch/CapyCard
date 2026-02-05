using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CapyCard.ViewModels
{
    public partial class LegalViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isOpen;

        [RelayCommand]
        private void Close()
        {
            IsOpen = false;
        }

        [RelayCommand]
        private void OpenUrl(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL '{url}': {ex.Message}");
            }
        }

        public void Show()
        {
            IsOpen = true;
        }
    }
}
