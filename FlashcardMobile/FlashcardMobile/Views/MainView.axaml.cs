using Avalonia.Controls;
using Avalonia.Interactivity;
using FlashcardMobile.ViewModels;

namespace FlashcardMobile.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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
            e.Handled = vm.HandleHardwareBack();
        }
    }
}