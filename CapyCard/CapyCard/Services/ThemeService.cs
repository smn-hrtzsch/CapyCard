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
                // Create new ResourceInclude for Colors
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

                // 3. Zen Mode - Apply or Remove Overrides
                // We use index 1 for Zen Mode styles if they exist, or append.
                // Best practice: manage specific "slots" in MergedDictionaries.
                // Slot 0: Colors (managed above)
                // Slot 1: Zen Mode (managed here)
                
                var zenSource = new Uri("avares://CapyCard/Styles/Themes/ZenMode.axaml");
                
                // Remove existing Zen Mode if present
                for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var dict = app.Resources.MergedDictionaries[i] as ResourceInclude;
                    if (dict?.Source == zenSource)
                    {
                        app.Resources.MergedDictionaries.RemoveAt(i);
                    }
                    // Also check Styles collection for StyleInclude
                    // ZenMode.axaml contains <Styles>, so it should be added to Application.Styles, not Resources!
                }
                
                // Handle Zen Mode Styles in Application.Styles
                // Slot: We assume we can append to Styles.
                var zenStyleSource = new Uri("avares://CapyCard/Styles/Themes/ZenMode.axaml");
                
                // First remove any existing Zen styles
                for (int i = app.Styles.Count - 1; i >= 0; i--)
                {
                    if (app.Styles[i] is StyleInclude include && include.Source == zenStyleSource)
                    {
                        app.Styles.RemoveAt(i);
                    }
                }

                if (isZen)
                {
                    var zenStyle = new StyleInclude(new Uri("avares://CapyCard/App.axaml"))
                    {
                        Source = zenStyleSource
                    };
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
