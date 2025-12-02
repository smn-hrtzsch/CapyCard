using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CapyCard.Services;
using CapyCard.ViewModels;
using System;
using System.Threading.Tasks;

namespace CapyCard.Views
{
        public partial class DeckDetailView : UserControl
        {
            private DeckDetailViewModel? _viewModel;
            private double _lastKeyboardHeight;
    
            public static readonly StyledProperty<bool> IsCompactModeProperty =
                AvaloniaProperty.Register<DeckDetailView, bool>(nameof(IsCompactMode));
    
            public bool IsCompactMode
            {
                get => GetValue(IsCompactModeProperty);
                set => SetValue(IsCompactModeProperty, value);
            }
    
            public DeckDetailView()
            {
                InitializeComponent();
                DataContextChanged += OnDataContextChanged;
                SizeChanged += OnSizeChanged;
            }
    
            private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
            {
                IsCompactMode = e.NewSize.Width < 800;
            }
    
            private void OnDataContextChanged(object? sender, EventArgs e)
            {
                if (_viewModel != null)
                {
                    _viewModel.RequestFrontFocus -= HandleRequestFrontFocus;
                    _viewModel.OnSubDeckAdded -= HandleSubDeckAdded;
                }
    
                if (DataContext is DeckDetailViewModel vm)
                {
                    _viewModel = vm;
                    _viewModel.RequestFrontFocus += HandleRequestFrontFocus;
                    _viewModel.OnSubDeckAdded += HandleSubDeckAdded;
                }
                else
                {
                    _viewModel = null;
                }
            }
    
            protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
            {
                base.OnAttachedToVisualTree(e);
                
                KeyboardService.KeyboardHeightChanged += OnKeyboardHeightChanged;
                SubDeckTextBox.GotFocus += OnSubDeckInputGotFocus;
                SubDeckTextBox.LostFocus += OnSubDeckInputLostFocus;
                // Use Tunnel to catch Enter before TextBox inserts a newline
                SubDeckTextBox.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            }
    
            protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
            {
                base.OnDetachedFromVisualTree(e);
    
                KeyboardService.KeyboardHeightChanged -= OnKeyboardHeightChanged;
                SubDeckTextBox.GotFocus -= OnSubDeckInputGotFocus;
                SubDeckTextBox.LostFocus -= OnSubDeckInputLostFocus;
                SubDeckTextBox.RemoveHandler(KeyDownEvent, OnInputKeyDown);
    
                if (_viewModel != null)
                {
                    _viewModel.RequestFrontFocus -= HandleRequestFrontFocus;
                    _viewModel.OnSubDeckAdded -= HandleSubDeckAdded;
                }
            }
    
            private void OnKeyboardHeightChanged(object? sender, double height)
            {
                _lastKeyboardHeight = height;
                
                Dispatcher.UIThread.Post(() =>
                {
                    if (IsCompactMode && SubDeckTextBox.IsFocused)
                    {
                        InputContainer.Margin = new Thickness(0, 0, 0, height);
                    }
                    else
                    {
                        InputContainer.Margin = new Thickness(0);
                    }
                });
            }
    
            private void OnSubDeckInputGotFocus(object? sender, GotFocusEventArgs e)
            {
                if (IsCompactMode && _lastKeyboardHeight > 0)
                {
                    InputContainer.Margin = new Thickness(0, 0, 0, _lastKeyboardHeight);
                }
            }
    
            private void OnSubDeckInputLostFocus(object? sender, RoutedEventArgs e)
            {
                InputContainer.Margin = new Thickness(0);
            }
    
            private void OnInputKeyDown(object? sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (_viewModel != null && _viewModel.AddSubDeckCommand.CanExecute(null))
                    {
                        _viewModel.AddSubDeckCommand.Execute(null);
                    }
                    
                    e.Handled = true;
                    
                    if (IsCompactMode)
                    {
                        KeyboardService.ShowKeyboard();
                    }
                }
            }
    
            private void HandleRequestFrontFocus()
            {
                Dispatcher.UIThread.Post(() =>
                {
                    FrontEditor.FocusEditor();
                });
            }
    
            private async void HandleSubDeckAdded()
            {
                await Task.Delay(150);
                Dispatcher.UIThread.Post(() =>
                {
                    SubDeckTextBox.Focus();
                    SubDeckTextBox.CaretIndex = SubDeckTextBox.Text?.Length ?? 0;
                });
            }
        }}