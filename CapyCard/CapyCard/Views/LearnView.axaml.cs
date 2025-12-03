using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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
            
            // Use Tunneling to catch the wheel event before the ScrollViewer inside consumes it
            var overlay = this.FindControl<Grid>("ImagePreviewOverlay");
            if (overlay != null)
            {
                overlay.AddHandler(PointerWheelChangedEvent, OnOverlayPointerWheelChanged, RoutingStrategies.Tunnel);
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < 800;
        }
        
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null)
            {
                _topLevel.KeyDown += TopLevelOnKeyDown;
            }

            // Ensure Handler is attached (remove first to avoid duplicates)
            var overlay = this.FindControl<Grid>("ImagePreviewOverlay");
            if (overlay != null)
            {
                overlay.RemoveHandler(PointerWheelChangedEvent, OnOverlayPointerWheelChanged);
                overlay.AddHandler(PointerWheelChangedEvent, OnOverlayPointerWheelChanged, RoutingStrategies.Tunnel);
            }

            if (DataContext is LearnViewModel vm)
            {
                _subscribedViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
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
                if (DataContext is LearnViewModel vm)
                {
                    if (vm.IsImagePreviewOpen)
                    {
                        CalculateInitialZoom(vm);
                        // Focus the overlay to enable KeyBindings
                         Dispatcher.UIThread.Post(() =>
                         {
                             var overlay = this.FindControl<Grid>("ImagePreviewOverlay");
                             overlay?.Focus();
                         });
                    }
                    else
                    {
                        // Restore Focus when preview closes
                        FocusMainActionButton();
                    }
                }
            }
        }

        private void CalculateInitialZoom(LearnViewModel vm)
        {
            if (vm.PreviewImageSource is Bitmap bitmap && _topLevel != null)
            {
                var containerWidth = this.Bounds.Width;
                var containerHeight = this.Bounds.Height;
                if (containerWidth <= 0 || containerHeight <= 0) return;

                var targetWidth = containerWidth * 0.75;
                var targetHeight = containerHeight * 0.75;

                var imgWidth = bitmap.Size.Width;
                var imgHeight = bitmap.Size.Height;
                if (imgWidth <= 0 || imgHeight <= 0) return;

                var zoomX = targetWidth / imgWidth;
                var zoomY = targetHeight / imgHeight;
                var zoom = Math.Min(zoomX, zoomY);
                
                vm.ImageZoomLevel = zoom;
            }
        }

        private void TopLevelOnKeyDown(object? sender, KeyEventArgs e)
        {
            if (!IsEffectivelyVisible || DataContext is not LearnViewModel vm) return;
            
            if (vm.IsImagePreviewOpen)
            {
                if (e.Key == Key.Escape)
                {
                    vm.CloseImagePreviewCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                
                // Handle Cmd/Strg + 0 for Reset Zoom
                var modifiers = e.KeyModifiers;
                bool isCtrl = (modifiers & KeyModifiers.Control) != 0;
                bool isMeta = (modifiers & KeyModifiers.Meta) != 0;
                
                if ((isCtrl || isMeta) && e.Key == Key.D0)
                {
                    CalculateInitialZoom(vm);
                    e.Handled = true;
                    return;
                }
                
                return; // Don't handle other keys like Enter
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
             // Focus overlay to receive keys
            if (sender is Control control)
            {
                control.Focus();
            }
            
            // Close on background click
            if (DataContext is LearnViewModel vm && vm.IsImagePreviewOpen)
            {
                if (sender is Grid grid && e.Source == grid)
                {
                     vm.CloseImagePreviewCommand.Execute(null);
                }
            }
        }

        private void OnOverlayPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;

            var modifiers = e.KeyModifiers;
            bool isCtrl = (modifiers & KeyModifiers.Control) != 0;
            bool isMeta = (modifiers & KeyModifiers.Meta) != 0;
            
            if (isCtrl || isMeta)
            {
                double zoomFactor = 0.05; 
                double delta = e.Delta.Y;
                vm.ImageZoomLevel += delta * zoomFactor;
                e.Handled = true;
            }
        }
    }
}