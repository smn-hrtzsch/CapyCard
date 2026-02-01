using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Linq;

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

                // 3. Zen Mode - Apply or Remove Overrides via Application Styles
                var zenUri = "avares://CapyCard/Styles/Themes/ZenMode.axaml";
                
                // Robust removal: find any StyleInclude that points to ZenMode.axaml
                // We compare the Source.ToString()
                for (int i = app.Styles.Count - 1; i >= 0; i--)
                {
                    if (app.Styles[i] is StyleInclude include && include.Source != null && 
                        (include.Source.ToString().Contains("ZenMode.axaml") || include.Source.ToString().EndsWith("ZenMode.axaml")))
                    {
                        app.Styles.RemoveAt(i);
                    }
                }

                if (isZen)
                {
                    // Create a fresh instance to be safe
                    var zenStyle = new StyleInclude(new Uri("avares://CapyCard/App.axaml"))
                    {
                        Source = new Uri(zenUri)
                    };
                    
                    // Add to the end for highest priority
                    app.Styles.Add(zenStyle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load theme {color} or Zen Mode: {ex.Message}");
            }
        }
    }
}
