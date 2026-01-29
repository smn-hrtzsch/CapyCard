using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapyCard.ViewModels
{
    public partial class FormatInfoViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isVisible;
        [ObservableProperty]
        private bool _isAiFormatDetailsExpanded;
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
        private void ToggleAiFormatDetails()
        {
            IsAiFormatDetailsExpanded = !IsAiFormatDetailsExpanded;
        }
        [RelayCommand]
        private void HandleEscape()
        {
            Close();
        }

        [RelayCommand]
        private void HandleEnter()
        {
            Close();
        }

        [RelayCommand]
        private void Close()
        {
            IsVisible = false;
            IsAiFormatDetailsExpanded = false;
        }
        public void Show()
        {
            IsVisible = true;
            IsAiFormatDetailsExpanded = false;
        }
    }
}
