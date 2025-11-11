using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlashcardApp.ViewModels; // Hinzufügen

namespace FlashcardApp
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Hier ist die wichtige Änderung:
                // Wir setzen den DataContext (das ViewModel) für unser Hauptfenster
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel() // Diese Zeile ist neu
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}