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
        
        // Pinch Gesture State
        private readonly Dictionary<int, Point> _activePointers = new();
        private double _previousPinchDistance = 0;

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
                
                // Manual Pinch Detection Handlers
                overlay.AddHandler(PointerPressedEvent, OnOverlayPointerPressed, RoutingStrategies.Tunnel);
                overlay.AddHandler(PointerMovedEvent, OnOverlayPointerMoved, RoutingStrategies.Tunnel);
                overlay.AddHandler(PointerReleasedEvent, OnOverlayPointerReleased, RoutingStrategies.Tunnel);
                overlay.AddHandler(PointerCaptureLostEvent, OnOverlayPointerCaptureLost, RoutingStrategies.Tunnel);
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
                
                // Re-attach pointer handlers just in case
                overlay.RemoveHandler(PointerPressedEvent, OnOverlayPointerPressed);
                overlay.AddHandler(PointerPressedEvent, OnOverlayPointerPressed, RoutingStrategies.Tunnel);
                overlay.RemoveHandler(PointerMovedEvent, OnOverlayPointerMoved);
                overlay.AddHandler(PointerMovedEvent, OnOverlayPointerMoved, RoutingStrategies.Tunnel);
                overlay.RemoveHandler(PointerReleasedEvent, OnOverlayPointerReleased);
                overlay.AddHandler(PointerReleasedEvent, OnOverlayPointerReleased, RoutingStrategies.Tunnel);
                overlay.RemoveHandler(PointerCaptureLostEvent, OnOverlayPointerCaptureLost);
                overlay.AddHandler(PointerCaptureLostEvent, OnOverlayPointerCaptureLost, RoutingStrategies.Tunnel);
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
                        _activePointers.Clear(); // Reset gestures
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
            
            if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;

            // Add pointer
            _activePointers[e.Pointer.Id] = e.GetPosition(this);

            // If exactly two pointers, initialize pinch distance
            if (_activePointers.Count == 2)
            {
                var points = _activePointers.Values.ToList();
                _previousPinchDistance = GetDistance(points[0], points[1]);
            }
            
            // Handle Close on Click logic (only if not part of a multi-touch gesture)
            // We wait for release to decide if it was a click or part of gesture?
            // For now, let's keep the simple click-to-close behavior ONLY if 1 pointer and no movement happened?
            // Or simpler: If we click on the background grid (source check), we close. 
            // BUT PointerPressed is Tunneling now, so we get it first. 
            // Let's check e.Source.
            if (_activePointers.Count == 1 && sender is Grid grid && e.Source == grid)
            {
                // We don't execute close here immediately because it might be start of pinch.
                // We can execute on Release if count stayed 1?
                // Or we accept that clicking background closes, but 2 finger tap might be tricky.
                // Let's stick to "Release closes if no pinch happened".
            }
        }
        
        private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
        {
            if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;

            if (_activePointers.ContainsKey(e.Pointer.Id))
            {
                _activePointers[e.Pointer.Id] = e.GetPosition(this);
                
                if (_activePointers.Count == 2)
                {
                    var points = _activePointers.Values.ToList();
                    double currentDistance = GetDistance(points[0], points[1]);
                    
                    if (Math.Abs(_previousPinchDistance) > 0.1) // Avoid division by zero
                    {
                        double scale = currentDistance / _previousPinchDistance;
                        
                        // Apply scale to zoom
                        // We might want to dampen it slightly or clamp
                        if (Math.Abs(1.0 - scale) > 0.01) // threshold to reduce jitter
                        {
                             vm.ImageZoomLevel *= scale;
                             _previousPinchDistance = currentDistance;
                             e.Handled = true;
                        }
                    }
                }
            }
        }
        
        private void OnOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_activePointers.ContainsKey(e.Pointer.Id))
            {
                _activePointers.Remove(e.Pointer.Id);
            }
            
            // Logic for closing on background click
            // If this was the last pointer, and we were on the grid... 
            // But tracking "was it a pinch" is hard here without more state.
            // Let's fall back to: explicit close button is there. Background click close is nice-to-have.
            // The original code handled click via PointerPressed on the Grid (Bubbling usually).
            // We switched to Tunneling handlers.
            // Let's try to support simple click-to-close.
            if (DataContext is LearnViewModel vm && vm.IsImagePreviewOpen)
            {
                if (sender is Grid grid && e.Source == grid && _activePointers.Count == 0)
                {
                     // Only close if we didn't just pinch. 
                     // We can check if zoom changed? Or just assume if it's a quick tap?
                     // Let's simply close for now, user can re-open. 
                     // Pinching usually ends with release one by one.
                     // If I release one finger, count is 1. Release second, count 0.
                     // If I tap, count 1 -> 0.
                     // So this logic might close the window after a pinch if the last finger release is on the background.
                     // It's better to check e.InitialPressMouseButton or something?
                     // Let's rely on the original "PointerPressed" event defined in XAML for closing?
                     // I removed "PointerPressed" from XAML attributes in previous step? No, I kept it.
                     // But I added a Tunneling handler for PointerPressed here too.
                     
                     // Let's let the XAML-defined Bubbling PointerPressed handler manage closing!
                     // But wait, I'm consuming/handling events in Move?
                     // If I don't set Handled=true in Pressed, it bubbles.
                }
            }
        }
        
        private void OnOverlayPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
             if (_activePointers.ContainsKey(e.Pointer.Id))
            {
                _activePointers.Remove(e.Pointer.Id);
            }
        }

        private double GetDistance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void OnOverlayPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;

            var modifiers = e.KeyModifiers;
            bool isCtrl = (modifiers & KeyModifiers.Control) != 0;
            bool isMeta = (modifiers & KeyModifiers.Meta) != 0;
            
            if (isCtrl || isMeta)
            {
                double zoomFactor = 0.05; // More sensitivity for smooth trackpad (was 0.1)
                // Wait, user said "Zoomstufen verkleinern (0.05)" for Buttons.
                // For trackpad/wheel, it depends on Delta.
                // Delta is usually small for trackpads.
                // Let's try 0.05 scale factor per Delta unit.
                
                double delta = e.Delta.Y;
                vm.ImageZoomLevel += delta * zoomFactor;
                e.Handled = true;
            }
        }
    }
}