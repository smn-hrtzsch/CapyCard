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
                // Register handlers on TopLevel (Window) just like in DeckDetailView
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownTunnel, RoutingStrategies.Tunnel);
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownBubble, RoutingStrategies.Bubble);
            }
            
            // Set initial focus
            Dispatcher.UIThread.Post(() => {
                this.Focus();
                FocusMainActionButton();
            }, DispatcherPriority.Loaded);

            // Wire up image preview handlers
            var overlay = this.FindControl<Grid>("ImagePreviewOverlay");
            if (overlay != null)
            {
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
                scrollViewer.AddHandler(Gestures.PinchEvent, OnGesturePinch);
            }

            if (DataContext is LearnViewModel vm)
            {
                _subscribedViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void FocusMainActionButton()
        {
            if (!this.IsVisible) return;

            var btnStandard = this.FindControl<Button>("ShowBackBtnStandard");
            var btnSmart = this.FindControl<Button>("ShowBackBtnSmart");
            var btnNext = this.FindControl<Button>("NextCardBtnStandard");
            var btnRate3 = this.FindControl<Button>("Rate3Btn");

            if (btnStandard != null && btnStandard.IsEffectivelyVisible) btnStandard.Focus();
            else if (btnSmart != null && btnSmart.IsEffectivelyVisible) btnSmart.Focus();
            else if (btnNext != null && btnNext.IsEffectivelyVisible) btnNext.Focus();
            else if (btnRate3 != null && btnRate3.IsEffectivelyVisible) btnRate3.Focus();
            else this.Focus();
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
                        Dispatcher.UIThread.Post(() => FocusMainActionButton());
                    }
                }
            }
            else if (e.PropertyName == nameof(LearnViewModel.IsEditing))
            {
                if (DataContext is LearnViewModel vm && !vm.IsEditing)
                {
                    Dispatcher.UIThread.Post(() => FocusMainActionButton());
                }
            }
        }

        private void TopLevelOnKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            if (!this.IsVisible) return;

            if (e.Key == Key.Escape)
            {
                var focused = _topLevel?.FocusManager?.GetFocusedElement();
                bool isInsideTextBox = focused is TextBox || (focused is Visual v && v.FindAncestorOfType<TextBox>() != null);

                if (isInsideTextBox)
                {
                    _topLevel?.FocusManager?.ClearFocus();
                    this.Focus();
                    e.Handled = true;
                }
            }
        }

        private void TopLevelOnKeyDownBubble(object? sender, KeyEventArgs e)
        {
            if (e.Handled || !this.IsVisible || DataContext is not LearnViewModel vm) return;
            
            if (e.Key == Key.Escape)
            {
                if (vm.IsImagePreviewOpen)
                {
                    vm.CloseImagePreviewCommand.Execute(null);
                }
                else if (vm.IsEditing)
                {
                    vm.CancelEditCommand.Execute(null);
                    this.Focus();
                }
                else
                {
                    vm.GoBackCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Enter or Key.Return or Key.Space)
            {
                var focused = _topLevel?.FocusManager?.GetFocusedElement();
                bool isInsideTextBox = focused is TextBox || (focused is Visual v && v.FindAncestorOfType<TextBox>() != null);

                if (!isInsideTextBox)
                {
                    if (vm.AdvanceCommand.CanExecute(null))
                    {
                        vm.AdvanceCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
            
            if (vm.IsImagePreviewOpen)
            {
                var modifiers = e.KeyModifiers;
                if (((modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0) && e.Key == Key.D0)
                {
                    CalculateInitialZoom(vm);
                    e.Handled = true;
                }
            }
        }

        private void CalculateInitialZoom(LearnViewModel vm)
        {
            if (vm.PreviewImageSource is Bitmap bitmap)
            {
                var containerWidth = this.Bounds.Width;
                var containerHeight = this.Bounds.Height;
                if (containerWidth <= 0 || containerHeight <= 0) return;

                var targetWidth = containerWidth * 0.75;
                var targetHeight = containerHeight * 0.75;

                var imgWidth = bitmap.Size.Width;
                var imgHeight = bitmap.Size.Height;
                if (imgWidth <= 0 || imgHeight <= 0) return;

                var zoom = Math.Min(targetWidth / imgWidth, targetHeight / imgHeight);
                vm.ImageZoomLevel = zoom;
                vm.DefaultZoomLevel = zoom;
            }
        }

        private void OnOverlayPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;
            var modifiers = e.KeyModifiers;
            if ((modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0)
            {
                vm.ImageZoomLevel += e.Delta.Y * 0.05;
                e.Handled = true;
            }
        }

        private readonly Dictionary<int, Point> _activePointers = new();
        private double _initialPinchDistance = -1;
        private double _initialZoomLevel = -1;

        private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;
            if (sender is Control control) control.Focus();
            if (e.ClickCount == 2 && e.Source is Image)
            {
                 if (vm.ImageZoomLevel > vm.DefaultZoomLevel * 1.1) CalculateInitialZoom(vm);
                 else vm.ImageZoomLevel *= 1.75;
                 e.Handled = true;
                 return;
            }
            _activePointers[e.Pointer.Id] = e.GetPosition(this);
            if (_activePointers.Count == 2)
            {
                var points = _activePointers.Values.ToList();
                _initialPinchDistance = Distance(points[0], points[1]);
                _initialZoomLevel = vm.ImageZoomLevel;
                e.Handled = true; 
            }
        }

        private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
        {
             if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;
             if (_activePointers.ContainsKey(e.Pointer.Id))
             {
                 _activePointers[e.Pointer.Id] = e.GetPosition(this);
                 if (_activePointers.Count == 2 && _initialPinchDistance > 0)
                 {
                     var points = _activePointers.Values.ToList();
                     vm.ImageZoomLevel = _initialZoomLevel * (1.0 + (Distance(points[0], points[1]) / _initialPinchDistance - 1.0) * 0.05);
                     CenterScrollViewer();
                     e.Handled = true;
                 }
             }
        }

        private void OnOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
             if (DataContext is not LearnViewModel vm || !vm.IsImagePreviewOpen) return;
             _activePointers.Remove(e.Pointer.Id);
             if (_activePointers.Count < 2) _initialPinchDistance = -1;
             if (_activePointers.Count == 0 && e.InitialPressMouseButton == MouseButton.Left && sender is Grid grid && e.Source == grid && _initialPinchDistance < 0) 
             {
                vm.CloseImagePreviewCommand.Execute(null);
             }
        }
        
        private double Distance(Point p1, Point p2) => Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

        private void OnGesturePinch(object? sender, PinchEventArgs e)
        {
            if (DataContext is LearnViewModel vm && vm.IsImagePreviewOpen)
            {
                vm.ImageZoomLevel *= 1.0 + (e.Scale - 1.0) * 0.05;
                CenterScrollViewer();
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
                if (horizontalOffset > 0) scrollViewer.Offset = new Vector(horizontalOffset, scrollViewer.Offset.Y);
                if (verticalOffset > 0) scrollViewer.Offset = new Vector(scrollViewer.Offset.X, verticalOffset);
            }
        }
    }
}
