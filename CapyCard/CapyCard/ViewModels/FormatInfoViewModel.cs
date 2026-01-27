using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapyCard.ViewModels
{
    public partial class FormatInfoViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isVisible;

        [RelayCommand]
        private void Close()
        {
            IsVisible = false;
        }

        public void Show()
        {
            IsVisible = true;
        }
    }
}
