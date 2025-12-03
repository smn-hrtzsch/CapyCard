using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CapyCard.ViewModels;

namespace CapyCard.Views
{
    public partial class LearnView : UserControl
    {
        private TopLevel? _topLevel;
        private LearnViewModel? _subscribedViewModel;

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<LearnView, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public LearnView()
        {
            InitializeComponent();
            this.SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < 800;
        }

        private void OnNavigationButtonClick(object? sender, RoutedEventArgs e)
        {
            FocusMainActionButton();
        }

        private void FocusMainActionButton()
        {
            // Delay focus change slightly to allow UI to update (e.g. button visibility)
            Dispatcher.UIThread.Post(() =>
            {
                var btnStandard = this.FindControl<Button>("ShowBackBtnStandard");
                var btnSmart = this.FindControl<Button>("ShowBackBtnSmart");

                if (btnStandard != null && btnStandard.IsEffectivelyVisible)
                {
                    btnStandard.Focus();
                }
                else if (btnSmart != null && btnSmart.IsEffectivelyVisible)
                {
                    btnSmart.Focus();
                }
            }, DispatcherPriority.Input);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null)
            {
                _topLevel.KeyDown += TopLevelOnKeyDown;
            }

            if (DataContext is LearnViewModel vm)
            {
                _subscribedViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            if (_topLevel != null)
            {
                _topLevel.KeyDown -= TopLevelOnKeyDown;
                _topLevel = null;
            }

            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _subscribedViewModel = null;
            }
        }
        
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            
            // Handle dynamic DataContext changes while attached
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _subscribedViewModel = null;
            }
            
            if (DataContext is LearnViewModel vm)
            {
                _subscribedViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LearnViewModel.IsImagePreviewOpen))
            {
                if (DataContext is LearnViewModel vm && !vm.IsImagePreviewOpen)
                {
                    // Restore Focus when preview closes
                    FocusMainActionButton();
                }
            }
        }

        private void TopLevelOnKeyDown(object? sender, KeyEventArgs e)
        {
            if (!IsEffectivelyVisible)
            {
                return;
            }

            if (DataContext is not LearnViewModel vm)
            {
                return;
            }
            
            // If Image Preview is open, consume Escape to close it (though the Button HotKey might handle it, 
            // explicit handling ensures safety if focus is weird)
            if (vm.IsImagePreviewOpen && e.Key == Key.Escape)
            {
                vm.CloseImagePreviewCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Don't handle Enter if Preview is open (let it be focused on controls if any)
            // But usually we want to prevent "Advance" if preview is open
            if (vm.IsImagePreviewOpen)
            {
                return;
            }

            if (e.Key is Key.Enter or Key.Return or Key.Space)
            {
                if (vm.AdvanceCommand.CanExecute(null))
                {
                    vm.AdvanceCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Close when clicking on the background grid (not the content)
            if (sender is Grid grid && e.Source == grid && DataContext is LearnViewModel vm)
            {
                if (vm.IsImagePreviewOpen)
                {
                     vm.CloseImagePreviewCommand.Execute(null);
                }
            }
        }
    }
}