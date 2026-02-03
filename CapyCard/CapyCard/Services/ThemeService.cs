using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;

namespace CapyCard.Services
{
    public class ThemeService
    {
        public void ApplyTheme(string color, string mode, bool isZen)
        {
            var app = Application.Current;
            if (app == null) return;

            // 1. Theme Mode
            var themeVariant = mode switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
            
            app.RequestedThemeVariant = themeVariant;

            if (app.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    window.RequestedThemeVariant = themeVariant;
                }

                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.RequestedThemeVariant = themeVariant;
                }
            }

            // 2. Determine Color Palette Source
            if (string.IsNullOrWhiteSpace(color)) color = "Teal";
            var colorUri = new Uri($"avares://CapyCard/Styles/Themes/Colors/{color}.axaml");
            
            try
            {
                var newTheme = new ResourceInclude(new Uri("avares://CapyCard/App.axaml"))
                {
                    Source = colorUri
                };

                if (app.Resources.MergedDictionaries.Count > 0)
                {
                    app.Resources.MergedDictionaries[0] = newTheme;
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(newTheme);
                }

                // 3. Zen Mode - styles are always loaded; toggle via a class
                // Adding/removing StyleIncludes at runtime can leave already-templated controls in a stale visual state.
                if (app.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop2)
                {
                    foreach (Window window in desktop2.Windows)
                    {
                        window.Classes.Set("zen", isZen);
                    }

                    if (desktop2.MainWindow != null)
                    {
                        desktop2.MainWindow.Classes.Set("zen", isZen);
                    }
                }

                if (app.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime single &&
                    single.MainView is StyledElement main)
                {
                    main.Classes.Set("zen", isZen);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load theme {color} or Zen Mode: {ex.Message}");
            }
        }
    }
}
