using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapyCard.ViewModels
{
    public partial class ImportHelpViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isVisible;
        [ObservableProperty]
        private bool _isFormatDetailsExpanded;
        public string JsonExample { get; } = @"{
  ""name"": ""Thema"",
  ""cards"": [
    { ""front"": ""Frage"", ""back"": ""Antwort"" }
  ],
  ""subDecks"": [
    { ""name"": ""Unterthema"", ""cards"": [...] }
  ]
}";
        [RelayCommand]
        private void ToggleFormatDetails()
        {
            IsFormatDetailsExpanded = !IsFormatDetailsExpanded;
        }
        [RelayCommand]
        private void Close()
        {
            IsVisible = false;
            IsFormatDetailsExpanded = false;
        }
        public void Show()
        {
            IsVisible = true;
            IsFormatDetailsExpanded = false;
        }
    }
}
