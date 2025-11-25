using Avalonia.Controls;
using Avalonia.Interactivity;
using FlashcardMobile.ViewModels;

namespace FlashcardMobile.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.BackRequested += OnBackRequested;
        }
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
}