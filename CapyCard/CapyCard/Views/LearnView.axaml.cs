using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
            IsCompactMode = e.NewSize.Width < AppConstants.DefaultThreshold;
        }
        
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null)
            {
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownTunnel, RoutingStrategies.Tunnel);
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownBubble, RoutingStrategies.Bubble);
            }
            
            // Ensure Handler is attached
            var overlay = this.FindControl<Grid>("ImagePreviewOverlay");
            if (overlay != null)
            {
                overlay.RemoveHandler(PointerWheelChangedEvent, OnOverlayPointerWheelChanged);
                overlay.AddHandler(PointerWheelChangedEvent, OnOverlayPointerWheelChanged, RoutingStrategies.Tunnel);

                overlay.AddHandler(PointerPressedEvent, OnOverlayPointerPressed, RoutingStrategies.Tunnel);
                overlay.AddHandler(PointerMovedEvent, OnOverlayPointerMoved, RoutingStrategies.Tunnel);
                overlay.AddHandler(PointerReleasedEvent, OnOverlayPointerReleased, RoutingStrategies.Tunnel);
                overlay.AddHandler(InputElement.PointerCaptureLostEvent, OnOverlayPointerReleased, RoutingStrategies.Bubble);

                overlay.AddHandler(Gestures.PinchEvent, OnGesturePinch);
            }
            
            var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
            if (scrollViewer != null)
            {
                scrollViewer.RemoveHandler(Gestures.PinchEvent, OnGesturePinch);
                scrollViewer.AddHandler(Gestures.PinchEvent, OnGesturePinch);
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
                _topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownTunnel);
                _topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownBubble);
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
                         Dispatcher.UIThread.Post(() =>
                         {
                             var overlay = this.FindControl<Grid>("ImagePreviewOverlay");
                             overlay?.Focus();
                         });
                    }
                    else
                    {
                        _activePointers.Clear();
                        _initialPinchDistance = -1;
                        _initialZoomLevel = -1;
                        FocusMainActionButton();
                    }
                }
            }
            else if (e.PropertyName == nameof(LearnViewModel.IsEditing))
            {
                if (DataContext is LearnViewModel vm && !vm.IsEditing)
                {
                    FocusMainActionButton();
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
                vm.DefaultZoomLevel = zoom;
            }
        }

        private void TopLevelOnKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            if (!IsEffectivelyVisible) return;

            if (e.Key == Key.Escape)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var focused = topLevel?.FocusManager?.GetFocusedElement();
                
                bool isInsideTextBox = focused is TextBox;
                if (!isInsideTextBox && focused is Visual v)
                {
                    isInsideTextBox = v.FindAncestorOfType<TextBox>() != null;
                }

                if (isInsideTextBox)
                {
                    topLevel?.FocusManager?.ClearFocus();
                    this.Focus();
                    e.Handled = true;
                }
            }
        }

        private void TopLevelOnKeyDownBubble(object? sender, KeyEventArgs e)
        {
            if (e.Handled || !IsEffectivelyVisible || DataContext is not LearnViewModel vm) return;
            
            if (vm.IsImagePreviewOpen)
            {
                if (e.Key == Key.Escape)
                {
                    vm.CloseImagePreviewCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                
                var modifiers = e.KeyModifiers;
                bool isCtrl = (modifiers & KeyModifiers.Control) != 0;
                bool isMeta = (modifiers & KeyModifiers.Meta) != 0;
                
                if ((isCtrl || isMeta) && e.Key == Key.D0)
                {
                    CalculateInitialZoom(vm);
                    e.Handled = true;
                    return;
                }
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (vm.IsEditing)
                {
                    vm.CancelEditCommand.Execute(null);
                    this.Focus();
                    e.Handled = true;
                }
                else
                {
                    if (vm.GoBackCommand.CanExecute(null))
                    {
                        vm.GoBackCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                return;
            }

            if (OperatingSystem.IsAndroid() && e.Key is Key.BrowserBack or Key.Back)
            {
                if (vm.GoBackCommand.CanExecute(null))
                {
                    vm.GoBackCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key is Key.Enter or Key.Return or Key.Space)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var focused = topLevel?.FocusManager?.GetFocusedElement();
                
                bool isInsideTextBox = focused is TextBox;
                if (!isInsideTextBox && focused is Visual v)
                {
                    isInsideTextBox = v.FindAncestorOfType<TextBox>() != null;
                }

                if (!isInsideTextBox)
                {
                    if (vm.AdvanceCommand.CanExecute(null))
                    {
                        vm.AdvanceCommand.Execute(null);
                        e.Handled = true;
                    }
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

        private readonly Dictionary<int, Point> _activePointers = new();
        private readonly Dictionary<int, Point> _initialPointerPositions = new();
        private double _initialPinchDistance = -1;
        private double _initialZoomLevel = -1;
        private Point _initialMidpoint;

        private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;

            if (sender is Control control)
            {
                control.Focus();
            }

            if (e.ClickCount == 2)
            {
                if (e.Source is Image)
                {
                     ToggleZoom(vm);
                     e.Handled = true;
                     return;
                }
            }
            
            var point = e.GetPosition(this);
            _activePointers[e.Pointer.Id] = point;
            _initialPointerPositions[e.Pointer.Id] = point;

            if (_activePointers.Count == 2)
            {
                var points = _activePointers.Values.ToList();
                _initialPinchDistance = Distance(points[0], points[1]);
                _initialMidpoint = new Point((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);
                _initialZoomLevel = vm.ImageZoomLevel;
                e.Handled = true; 
            }
        }

        private void ToggleZoom(LearnViewModel vm)
        {
              double current = vm.ImageZoomLevel;
              double defaultZoom = vm.DefaultZoomLevel;
              
              if (current > defaultZoom * 1.1) 
              {
                  CalculateInitialZoom(vm);
              }
              else
              {
                  vm.ImageZoomLevel = current * 1.75;
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
                     e.Handled = true;
                     
                     if (_initialPinchDistance > 0)
                     {
                         var points = _activePointers.Values.ToList();
                         var currentDistance = Distance(points[0], points[1]);
                         var currentMidpoint = new Point((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);
                         
                         var midpointMovement = Distance(_initialMidpoint, currentMidpoint);
                         var distanceChange = Math.Abs(currentDistance - _initialPinchDistance);
                         
                         bool isPinch = distanceChange > midpointMovement && distanceChange > 30;
                         
                         if (isPinch)
                         {
                             var rawZoomFactor = currentDistance / _initialPinchDistance;
                             var dampedZoomFactor = 1.0 + (rawZoomFactor - 1.0) * 0.05;
                             var newZoom = _initialZoomLevel * dampedZoomFactor;
                             vm.ImageZoomLevel = newZoom;
                             CenterScrollViewer();
                         }
                     }
                 }
             }
        }

        private void OnOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
             if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;

             if (_activePointers.ContainsKey(e.Pointer.Id))
             {
                 _activePointers.Remove(e.Pointer.Id);
             }

             if (_activePointers.Count < 2)
             {
                 _initialPinchDistance = -1;
                 _initialZoomLevel = -1;
             }
             
             if (_activePointers.Count == 0)
             {
                 _activePointers.Clear();
                 _initialPointerPositions.Clear();
                 _initialPinchDistance = -1;
                 _initialZoomLevel = -1;
             }

             if (_activePointers.Count == 0 && e.InitialPressMouseButton == MouseButton.Left)
             {
                 if (sender is Grid grid && e.Source == grid)
                 {
                     if (_initialPinchDistance < 0) 
                     {
                        vm.CloseImagePreviewCommand.Execute(null);
                     }
                 }
             }
        }
        
        private void OnResetZoomClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is LearnViewModel vm)
            {
                CalculateInitialZoom(vm);
            }
        }

        private double Distance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void OnGesturePinch(object? sender, PinchEventArgs e)
        {
            if (DataContext is LearnViewModel vm && vm.IsImagePreviewOpen)
            {
                if (Math.Abs(e.Scale - 1.0) > 0.02)
                {
                    var dampedScale = 1.0 + (e.Scale - 1.0) * 0.05;
                    var newZoom = vm.ImageZoomLevel * dampedScale;
                    vm.ImageZoomLevel = newZoom;
                    CenterScrollViewer();
                }
                e.Handled = true;
            }
        }

        private void CenterScrollViewer()
        {
            var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
            if (scrollViewer != null)
            {
                var horizontalOffset = (scrollViewer.Extent.Width - scrollViewer.Viewport.Width) / 2;
                var verticalOffset = (scrollViewer.Extent.Height - scrollViewer.Viewport.Height) / 2;
                
                if (horizontalOffset > 0)
                    scrollViewer.Offset = new Vector(horizontalOffset, scrollViewer.Offset.Y);
                if (verticalOffset > 0)
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, verticalOffset);
            }
        }
    }
}
