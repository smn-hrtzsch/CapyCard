using CapyCard.Models;
using CapyCard.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CapyCard.ViewModels
{
    public class ColorOption
    {
        public required string Name { get; set; }
        public required string Color { get; set; }
    }

    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly IUserSettingsService _settingsService;
        private readonly ThemeService _themeService;
        private UserSettings? _originalSettings;

        // Observable Properties linked to UI
        [ObservableProperty]
        private bool _isOpen;

        [ObservableProperty]
        private string _selectedColor;

        [ObservableProperty]
        private string _selectedMode;

        [ObservableProperty]
        private bool _isZenMode;

        [ObservableProperty]
        private bool _showEditorToolbar;

        // Available Options
        public ObservableCollection<ColorOption> ColorOptions { get; } = new()
        {
            new() { Name = "Teal", Color = "#018786" },
            new() { Name = "Blue", Color = "#2196F3" },
            new() { Name = "Green", Color = "#4CAF50" },
            new() { Name = "Red", Color = "#F44336" },
            new() { Name = "Orange", Color = "#FF9800" },
            new() { Name = "Purple", Color = "#9C27B0" },
            new() { Name = "Pink", Color = "#E91E63" },
            new() { Name = "Monochrome", Color = "#424242" }
        };
        
        public ObservableCollection<string> AvailableModes { get; } = new()
        {
            "System", "Light", "Dark"
        };

        public SettingsViewModel(IUserSettingsService settingsService, ThemeService themeService)
        {
            _settingsService = settingsService;
            _themeService = themeService;
            
            // Initial dummy values, LoadSettings will overwrite
            _selectedColor = "Teal";
            _selectedMode = "System";
            _isZenMode = false;
            _showEditorToolbar = true;
        }

        public async Task InitializeAsync()
        {
            var settings = await _settingsService.LoadSettingsAsync();
            
            // Store original settings for cancellation
            _originalSettings = new UserSettings
            {
                Id = settings.Id,
                ThemeColor = settings.ThemeColor,
                ThemeMode = settings.ThemeMode,
                IsZenMode = settings.IsZenMode,
                ShowEditorToolbar = settings.ShowEditorToolbar
            };

            SelectedColor = settings.ThemeColor;
            SelectedMode = settings.ThemeMode;
            IsZenMode = settings.IsZenMode;
            ShowEditorToolbar = settings.ShowEditorToolbar;
        }

        partial void OnSelectedColorChanged(string value) => ApplyTheme();
        partial void OnSelectedModeChanged(string value) => ApplyTheme();
        partial void OnIsZenModeChanged(bool value) => ApplyTheme();

        private void ApplyTheme()
        {
            _themeService.ApplyTheme(SelectedColor, SelectedMode, IsZenMode);
        }

        [RelayCommand]
        private void SetSelectedColor(string colorName)
        {
            SelectedColor = colorName;
        }

        [RelayCommand]
        private void Cancel()
        {
            if (_originalSettings != null)
            {
                // Restore settings (this triggers ApplyTheme via OnChanged handlers)
                SelectedColor = _originalSettings.ThemeColor;
                SelectedMode = _originalSettings.ThemeMode;
                IsZenMode = _originalSettings.IsZenMode;
                ShowEditorToolbar = _originalSettings.ShowEditorToolbar;
            }
            IsOpen = false;
        }

        [RelayCommand]
        private async Task Save()
        {
            var settings = new UserSettings
            {
                Id = 1,
                ThemeColor = SelectedColor,
                ThemeMode = SelectedMode,
                IsZenMode = IsZenMode,
                ShowEditorToolbar = ShowEditorToolbar
            };
            
            await _settingsService.SaveSettingsAsync(settings);
            IsOpen = false;
        }
    }
}
