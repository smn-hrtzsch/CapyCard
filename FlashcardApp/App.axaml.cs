using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlashcardApp.ViewModels; 

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
                desktop.MainWindow = new MainWindow
                {
                    // HIER DIE KORREKTUR:
                    // Wir müssen das 'MainViewModel' verwenden, nicht 'MainWindowViewModel'
                    DataContext = new MainViewModel() 
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}