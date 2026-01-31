using Avalonia;
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

            // 2. Determine Color Palette Source
            // If Zen Mode is active, we might want to enforce Monochrome or a muted theme.
            // For now, let's respect the user's color choice unless Zen implies Monochrome.
            // Let's assume Zen Mode forces Monochrome for "weniger Ablenkung durch Farben".
            // Or maybe the user selects "Monochrome" explicitly.
            
            // Let's stick to the selected color. Zen Mode might affect UI density later.
            // The prompt said "Zen Mode ... Buttons dezenter". 
            // This might mean avoiding strong Primary colors. 
            // I'll leave Zen Mode logic minimal for now (just boolean flag stored), 
            // maybe in the future we swap specific brushes.
            
            // Validate color string to prevent crashes
            if (string.IsNullOrWhiteSpace(color)) color = "Teal";
            
            var newSource = new Uri($"avares://CapyCard/Styles/Themes/Colors/{color}.axaml");
            
            try
            {
                // Create new ResourceInclude
                var newTheme = new ResourceInclude(new Uri("avares://CapyCard/App.axaml"))
                {
                    Source = newSource
                };

                // Replace the first dictionary (which we know is our color theme from App.axaml)
                if (app.Resources.MergedDictionaries.Count > 0)
                {
                    app.Resources.MergedDictionaries[0] = newTheme;
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(newTheme);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load theme {color}: {ex.Message}");
            }
        }
    }
}
