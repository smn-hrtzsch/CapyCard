using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using CapyCard.Services;
using CapyCard.ViewModels;
using CapyCard.Views;
using CapyCard.Data;
using CapyCard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using QuestPDF.Infrastructure;

namespace CapyCard;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. QuestPDF Initialisierung (Alte Version benötigt keine Lizenz)
        /*
        try 
        {
            QuestPDF.Settings.License = LicenseType.Community;
            Console.WriteLine("[App] QuestPDF License set to Community.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] QuestPDF Init Failed: {ex.ToString()}");
        }
        */

        // 2. Datenbank Migration (Alte DB verschieben falls vorhanden)
        MigrateOldDatabase();

        // 3. Datenbank Initialisierung
        InitializeDatabase();

        // 4. Clipboard Service Initialisierung (falls noch nicht von Plattform gesetzt)
        if (CapyCard.Services.ClipboardService.Current == null)
        {
            CapyCard.Services.ClipboardService.Current = new CapyCard.Services.DesktopClipboardService();
        }

        // 5. Load User Settings (apply after main view exists)
        UserSettings settings;
        try 
        {
            var userSettingsService = new UserSettingsService();
            settings = userSettingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] Failed to load settings: {ex.Message}");
            settings = new UserSettings();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        // 6. Apply Theme + Zen Mode after window/view is created
        try
        {
            var themeService = new ThemeService();
            themeService.ApplyTheme(settings.ThemeColor, settings.ThemeMode, settings.IsZenMode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] Failed to apply settings: {ex.Message}");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private void MigrateOldDatabase()
    {
        // Migration nur auf Desktop-Plattformen (Windows/macOS/Linux) sinnvoll,
        // da auf Mobile die Pfade anders funktionieren und die alte App dort nicht existierte.
        if (OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        try
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var basePath = Environment.GetFolderPath(folder);
            
            // Alter Pfad: Direkt im LocalApplicationData Ordner
            var oldDbPath = Path.Combine(basePath, "flashcards.db");
            
            // Neuer Pfad: Im Unterordner 'CapyCard'
            var newDir = Path.Combine(basePath, "CapyCard");
            var newDbPath = Path.Combine(newDir, "flashcards.db");

            // Nur migrieren, wenn alte DB existiert und neue noch NICHT
            if (File.Exists(oldDbPath) && !File.Exists(newDbPath))
            {
                Console.WriteLine($"[Migration] Found old database at {oldDbPath}. Moving to {newDbPath}...");

                // Sicherstellen, dass der Zielordner existiert
                if (!Directory.Exists(newDir))
                {
                    Directory.CreateDirectory(newDir);
                }
                
                // Datei verschieben
                File.Move(oldDbPath, newDbPath);
                Console.WriteLine($"[Migration] Database moved successfully.");
            }
        }
        catch (Exception ex)
        {
            // Fehler loggen, aber App-Start nicht verhindern. 
            // Im schlimmsten Fall wird eine neue DB erstellt.
            Console.WriteLine($"[Migration] Failed to migrate database: {ex.Message}");
        }
    }

    private void InitializeDatabase()
    {
        using (var db = new FlashcardDbContext())
        {
            if (OperatingSystem.IsBrowser())
            {
                // In-Memory DB braucht keine Migrationen, aber das Schema muss erstellt werden
                db.Database.EnsureCreated();
                return;
            }

            try
            {
                db.Database.Migrate();
            }
            catch (Exception ex)
            {
                // Ignoriere "Table already exists" Fehler, da wir eine existierende DB importiert haben
                if (!ex.Message.Contains("already exists"))
                {
                    Console.WriteLine($"Database Migration Failed: {ex.Message}");
                }
            }
        }
    }
}
