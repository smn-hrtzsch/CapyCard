using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using FlashcardMobile.ViewModels;
using FlashcardMobile.Views;
using FlashcardMobile.Data;
using FlashcardMobile.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using QuestPDF.Infrastructure;

namespace FlashcardMobile;

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

        // 2. Datenbank Initialisierung
        InitializeDatabase();

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

    private void InitializeDatabase()
    {
        using (var db = new FlashcardDbContext())
        {
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