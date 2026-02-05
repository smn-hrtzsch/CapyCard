using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using CapyCard.Models;
using CapyCard.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapyCard.ViewModels
{
    public partial class ColorOption : ObservableObject
    {
        public required string Name { get; set; }
        
        [ObservableProperty]
        private IBrush _previewBrush = Brushes.Transparent;

        [ObservableProperty]
        private bool _isSelected;
    }

    public partial class SettingsViewModel : ViewModelBase
    {
        private static readonly IReadOnlyDictionary<string, string> FallbackPreviewColors = new Dictionary<string, string>
        {
            ["Teal"] = "#00897B",
            ["Blue"] = "#1E88E5",
            ["Green"] = "#43A047",
            ["Monochrome"] = "#616161",
            ["Pink"] = "#D81B60",
            ["Purple"] = "#9C27B0",
            ["Red"] = "#D32F2F",
            ["Orange"] = "#EF6C00"
        };

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
        public ObservableCollection<ColorOption> ColorOptions { get; } = BuildColorOptions();
        
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
            UpdateColorPreviews(GetPreviewVariant());
        }

        private static ObservableCollection<ColorOption> BuildColorOptions()
        {
            var options = new ObservableCollection<ColorOption>();
            foreach (var (name, fallbackHex) in FallbackPreviewColors)
            {
                options.Add(new ColorOption
                {
                    Name = name,
                    PreviewBrush = Brush.Parse(fallbackHex)
                });
            }

            return options;
        }

        private void UpdateColorPreviews(ThemeVariant variant)
        {
            foreach (var option in ColorOptions)
            {
                option.PreviewBrush = LoadThemePreviewBrush(option.Name, variant);
            }
        }

        private ThemeVariant GetPreviewVariant()
        {
            return SelectedMode switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => Application.Current?.ActualThemeVariant ?? ThemeVariant.Light
            };
        }

        private static IBrush LoadThemePreviewBrush(string colorName, ThemeVariant variant)
        {
            var colorUri = new Uri($"avares://CapyCard/Styles/Themes/Colors/{colorName}.axaml");
            try
            {
                var dictionary = (ResourceDictionary)AvaloniaXamlLoader.Load(colorUri);

                if (dictionary.ThemeDictionaries.TryGetValue(variant, out var themeResources) &&
                    themeResources is ResourceDictionary variantDictionary)
                {
                    if (variantDictionary.TryGetResource("PrimaryColor", null, out var primaryColor) &&
                        primaryColor is Color resolvedColor)
                    {
                        return new SolidColorBrush(resolvedColor);
                    }

                    if (variantDictionary.TryGetResource("PrimaryBrush", null, out var primaryBrush) &&
                        primaryBrush is IBrush resolvedBrush)
                    {
                        return resolvedBrush;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load preview color for theme '{colorName}': {ex.Message}");
            }

            if (FallbackPreviewColors.TryGetValue(colorName, out var fallbackHex))
            {
                return Brush.Parse(fallbackHex);
            }

            return Brushes.Transparent;
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
            UpdateColorPreviews(GetPreviewVariant());
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
            UpdateColorPreviews(GetPreviewVariant());
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

        [RelayCommand]
        private void OpenUrl(string url)
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    System.Diagnostics.Process.Start("open", url);
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    System.Diagnostics.Process.Start("xdg-open", url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open URL '{url}': {ex.Message}");
            }
        }
    }
}
