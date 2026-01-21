using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CapyCard.ViewModels;

namespace CapyCard.Views;

public partial class MainView : UserControl
{
    private TopLevel? _topLevel;

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel == null) return;

        _topLevel.BackRequested += OnBackRequested;
        _topLevel.KeyDown += OnTopLevelKeyDown;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_topLevel != null)
        {
            _topLevel.BackRequested -= OnBackRequested;
            _topLevel.KeyDown -= OnTopLevelKeyDown;
            _topLevel = null;
        }
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            e.Handled = vm.HandleHardwareBack();
        }
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Some Android back interactions can surface as Key events rather than BackRequested.
        if (e.Key is not (Key.BrowserBack or Key.Back or Key.Escape))
        {
            return;
        }

        if (DataContext is MainViewModel vm)
        {
            var handled = vm.HandleHardwareBack();
            if (handled)
            {
                e.Handled = true;
            }
        }
    }
}