using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using CapyCard.ViewModels;
using System;
using System.IO;
using System.Text.Json;

namespace CapyCard.Views;

public partial class MainWindow : Window
{
    private static readonly string SettingsPath = GetSettingsPath();
    private bool _usedFallbackSize;
    
    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        this.Closing += OnClosing;
        
        // Set initial size based on screen or saved settings
        SetInitialWindowSize();
    }

    private static string GetSettingsPath()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var basePath = Environment.GetFolderPath(folder);
        var appFolder = Path.Combine(basePath, "CapyCard");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "window-settings.json");
    }

    private void SetInitialWindowSize()
    {
        // Try to load saved settings first
        if (TryLoadWindowSettings(out var settings))
        {
            Width = settings.Width;
            Height = settings.Height;
            
            // Position will be set after window is shown (in OnLoaded)
            // because we need to verify the position is still valid on current screen
        }
        else
        {
            _usedFallbackSize = !TrySetDefaultSize();
        }
    }

    private bool TrySetDefaultSize()
    {
        // Get primary screen size - this works before the window is shown
        var screen = Screens?.Primary;
        if (screen != null)
        {
            SetDefaultSizeFromScreen(screen);
            return true;
        }

        // Fallback if screens not available yet
        Width = 1000;
        Height = 800;
        return false;
    }

    private void SetDefaultSizeFromScreen(Screen screen)
    {
        var workingArea = screen.WorkingArea;
        var scaling = screen.Scaling;

        // Calculate 75% width and 85% height (accounting for DPI scaling)
        Width = (workingArea.Width / scaling) * 0.75;
        Height = (workingArea.Height / scaling) * 0.85;
    }

    private void CenterWindowOnScreen(Screen screen)
    {
        var workingArea = screen.WorkingArea;
        var scaling = screen.Scaling;
        var widthPx = Width * scaling;
        var heightPx = Height * scaling;
        var x = workingArea.X + (workingArea.Width - widthPx) / 2;
        var y = workingArea.Y + (workingArea.Height - heightPx) / 2;

        Position = new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.BackRequested += OnBackRequested;
        }
        
        // Restore position if we have saved settings
        if (TryLoadWindowSettings(out var settings))
        {
            // Verify the saved position is still valid (screen might have changed)
            if (IsPositionValid(settings.X, settings.Y, settings.Width, settings.Height))
            {
                Position = new PixelPoint(settings.X, settings.Y);
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
            // else: keep CenterScreen from XAML
        }
        else if (_usedFallbackSize)
        {
            var screen = Screens?.Primary;
            if (screen != null)
            {
                SetDefaultSizeFromScreen(screen);
                CenterWindowOnScreen(screen);
                _usedFallbackSize = false;
            }
        }
    }

    private bool IsPositionValid(int x, int y, double width, double height)
    {
        var screens = Screens;
        if (screens == null) return false;
        
        // Check if at least part of the window would be visible on any screen
        foreach (var screen in screens.All)
        {
            var bounds = screen.WorkingArea;
            // Window is valid if at least 100px would be visible on this screen
            if (x + width > bounds.X + 100 && 
                x < bounds.X + bounds.Width - 100 &&
                y + height > bounds.Y + 50 && 
                y < bounds.Y + bounds.Height - 50)
            {
                return true;
            }
        }
        return false;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowSettings();
    }

    private void SaveWindowSettings()
    {
        try
        {
            var settings = new WindowSettings
            {
                Width = Width,
                Height = Height,
                X = Position.X,
                Y = Position.Y
            };
            
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors - not critical
        }
    }

    private bool TryLoadWindowSettings(out WindowSettings settings)
    {
        settings = new WindowSettings();
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<WindowSettings>(json);
                if (loaded != null && loaded.Width > 100 && loaded.Height > 100)
                {
                    settings = loaded;
                    return true;
                }
            }
        }
        catch
        {
            // Ignore load errors - will use defaults
        }
        return false;
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // If the ViewModel handled the back action, set Handled to true
            // to prevent the app from closing.
            e.Handled = vm.HandleHardwareBack();
        }
    }
    
    private class WindowSettings
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}
