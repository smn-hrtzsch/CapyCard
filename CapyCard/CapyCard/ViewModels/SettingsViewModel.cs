using Avalonia.Media;
using CapyCard.Models;
using CapyCard.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CapyCard.ViewModels
{
    public partial class ColorOption : ObservableObject
    {
        public required string Name { get; set; }
        public required string Color { get; set; }

        [ObservableProperty]
        private bool _isSelected;
        
        public IBrush PreviewBrush => Brush.Parse(Color);
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

        public bool IsModeSystem
        {
            get => SelectedMode == "System";
            set { if (value) SelectedMode = "System"; OnPropertyChanged(); }
        }

        public bool IsModeLight
        {
            get => SelectedMode == "Light";
            set { if (value) SelectedMode = "Light"; OnPropertyChanged(); }
        }

        public bool IsModeDark
        {
            get => SelectedMode == "Dark";
            set { if (value) SelectedMode = "Dark"; OnPropertyChanged(); }
        }

        [ObservableProperty]
        private bool _isZenMode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HideEditorToolbar))]
        private bool _showEditorToolbar;

        public bool HideEditorToolbar
        {
            get => !ShowEditorToolbar;
            set => ShowEditorToolbar = !value;
        }

        // Available Options
        public ObservableCollection<ColorOption> ColorOptions { get; } = new()
        {
            new() { Name = "Teal", Color = "#00897B" },
            new() { Name = "Blue", Color = "#1E88E5" },
            new() { Name = "Green", Color = "#43A047" },
            new() { Name = "Monochrome", Color = "#757575" },
            new() { Name = "Pink", Color = "#D81B60" },
            new() { Name = "Purple", Color = "#8E24AA" },
            new() { Name = "Red", Color = "#D32F2F" },
            new() { Name = "Orange", Color = "#FF9800" }
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
            
            UpdateColorSelection();
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
            
            // Trigger property changes
            OnPropertyChanged(nameof(IsModeSystem));
            OnPropertyChanged(nameof(IsModeLight));
            OnPropertyChanged(nameof(IsModeDark));
            OnPropertyChanged(nameof(HideEditorToolbar));
        }

        partial void OnSelectedColorChanged(string value)
        {
            UpdateColorSelection();
            ApplyTheme();
        } 
        
        private void UpdateColorSelection()
        {
            foreach (var option in ColorOptions)
            {
                option.IsSelected = option.Name == SelectedColor;
            }
        }

        partial void OnSelectedModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsModeSystem));
            OnPropertyChanged(nameof(IsModeLight));
            OnPropertyChanged(nameof(IsModeDark));
            ApplyTheme();
        }

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
                ShowEditorToolbar = !HideEditorToolbar // Save inverted logic
            };
            
            await _settingsService.SaveSettingsAsync(settings);
            IsOpen = false;
        }
    }
}
