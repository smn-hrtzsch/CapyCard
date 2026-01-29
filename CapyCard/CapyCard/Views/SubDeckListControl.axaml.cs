using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CapyCard.Services;
using CapyCard.ViewModels;
using System;
using System.Threading.Tasks;

namespace CapyCard.Views
{
    public partial class SubDeckListControl : UserControl
    {
        private DeckDetailViewModel? _viewModel;
        private double _lastKeyboardHeight;
        
        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<SubDeckListControl, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public static readonly StyledProperty<double> FooterHeightProperty =
            AvaloniaProperty.Register<SubDeckListControl, double>(nameof(FooterHeight));

        public double FooterHeight
        {
            get => GetValue(FooterHeightProperty);
            private set => SetValue(FooterHeightProperty, value);
        }

        public SubDeckListControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnSizeChanged;
            
            ToggleContainer.SizeChanged += OnFooterElementSizeChanged;
            InputContainer.SizeChanged += OnFooterElementSizeChanged;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < AppConstants.StackingThreshold;
        }

        private void OnFooterElementSizeChanged(object? sender, SizeChangedEventArgs e)
        {
             // Calculate total height of the fixed footer area (Toggle Button + Input)
             // Toggle Button is in Row 0 but bottom aligned (so it sits on top of Input)
             // Input is in Row 1
             // Total Height = ToggleButton.Bounds.Height + InputContainer.Bounds.Height
             
             // We use Bounds.Height because e.NewSize might only be for one element
             // But be careful about Margins. ToggleContainer has Bottom Margin.
             
             var toggleHeight = ToggleContainer.Bounds.Height + ToggleContainer.Margin.Bottom + ToggleContainer.Margin.Top;
             var inputHeight = InputContainer.Bounds.Height + InputContainer.Margin.Bottom + InputContainer.Margin.Top;
             
             FooterHeight = toggleHeight + inputHeight;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.OnSubDeckAdded -= HandleSubDeckAdded;
            }

            if (DataContext is DeckDetailViewModel vm)
            {
                _viewModel = vm;
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

        private async void HandleSubDeckAdded()
        {
            await Task.Delay(150);
            Dispatcher.UIThread.Post(() =>
            {
                SubDeckTextBox.Focus();
                SubDeckTextBox.CaretIndex = SubDeckTextBox.Text?.Length ?? 0;
            });
        }
    }
}
