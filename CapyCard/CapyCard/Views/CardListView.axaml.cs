using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CapyCard.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using System.ComponentModel;

namespace CapyCard.Views
{
    public partial class CardListView : UserControl
    {
        private CardListViewModel? _boundViewModel;
        private TopLevel? _topLevel;
        private CardListViewModel? _subscribedViewModel;
        
        // Selection state
        private bool _isPointerSelecting;
        private bool _selectionTargetState;
        private int _anchorIndex = -1;
        private List<bool>? _originalSelection;
        private Control? _activeListControl; 

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<CardListView, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public CardListView()
        {
            InitializeComponent();
            
            this.DataContextChanged += OnDataContextChanged;
            this.SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < AppConstants.HeaderThreshold;
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

            // Image Preview Handlers
            var overlay = this.FindControl<Grid>("ImagePreviewOverlay");
            if (overlay != null)
            {
                overlay.AddHandler(PointerWheelChangedEvent, OnOverlayPointerWheelChanged, RoutingStrategies.Tunnel);
                overlay.AddHandler(PointerPressedEvent, OnOverlayPointerPressed, RoutingStrategies.Tunnel);
                overlay.AddHandler(PointerReleasedEvent, OnOverlayPointerReleased, RoutingStrategies.Tunnel);
            }

            if (DataContext is CardListViewModel vm)
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

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CardListViewModel.IsImagePreviewOpen))
            {
                if (DataContext is CardListViewModel vm)
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
                }
            }
        }

        private void CalculateInitialZoom(CardListViewModel vm)
        {
            if (vm.PreviewImageSource is Bitmap bitmap && _topLevel != null)
            {
                var containerWidth = this.Bounds.Width;
                var containerHeight = this.Bounds.Height;
                if (containerWidth <= 0 || containerHeight <= 0) return;

                var targetWidth = containerWidth * 0.85;
                var targetHeight = containerHeight * 0.85;

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
            if (DataContext is not CardListViewModel vm || !vm.IsImagePreviewOpen) return;
            var modifiers = e.KeyModifiers;
            if ((modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0)
            {
                vm.ImageZoomLevel += e.Delta.Y * 0.05;
                e.Handled = true;
            }
        }

        private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not CardListViewModel vm || !vm.IsImagePreviewOpen) return;
            if (sender is Control control) control.Focus();
            if (e.ClickCount == 2 && e.Source is Image)
            {
                 if (vm.ImageZoomLevel > vm.DefaultZoomLevel * 1.1) CalculateInitialZoom(vm);
                 else vm.ImageZoomLevel *= 1.75;
                 e.Handled = true;
            }
        }

        private void OnOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
             if (DataContext is not CardListViewModel vm || !vm.IsImagePreviewOpen) return;
             if (e.InitialPressMouseButton == MouseButton.Left && sender is Grid grid && e.Source == grid) 
             {
                vm.CloseImagePreviewCommand.Execute(null);
             }
        }

        private void TopLevelOnKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            if (!IsEffectivelyVisible || DataContext is not CardListViewModel vm) return;

            if (e.Key == Key.Escape)
            {
                // 1. Close Delete Confirmation if open
                if (vm.IsConfirmingDelete)
                {
                    vm.CancelDeleteCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // 2. Close Image Preview if open
                if (vm.IsImagePreviewOpen)
                {
                    vm.CloseImagePreviewCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // 3. Handle Editing Mode
                if (vm.IsEditing)
                {
                    var focused = _topLevel?.FocusManager?.GetFocusedElement();
                    bool isInsideTextBox = focused is TextBox;
                    if (!isInsideTextBox && focused is Visual v)
                    {
                        isInsideTextBox = v.FindAncestorOfType<TextBox>() != null;
                    }

                    if (isInsideTextBox)
                    {
                        // 1st Escape: Clear focus from the editor
                        _topLevel?.FocusManager?.ClearFocus();
                        this.Focus();
                        e.Handled = true;
                    }
                    else
                    {
                        // 2nd Escape: Cancel editing
                        vm.CancelEditCommand.Execute(null);
                        e.Handled = true;
                    }
                    return;
                }

                // 4. Close Card Preview if open
                if (vm.IsPreviewOpen)
                {
                    vm.ClosePreviewCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // 5. Handle focus clearing for other cases
                var focusedElement = _topLevel?.FocusManager?.GetFocusedElement();
                if (focusedElement is TextBox)
                {
                    _topLevel?.FocusManager?.ClearFocus();
                    this.Focus();
                    e.Handled = true;
                }
            }

            // CONFIRM DELETE SHORTCUT: Enter while confirming
            if (vm.IsConfirmingDelete && e.Key == Key.Enter)
            {
                if (vm.ConfirmDeleteCommand.CanExecute(null))
                {
                    vm.ConfirmDeleteCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // SAVE SHORTCUT: Cmd/Ctrl + Enter while editing
            if (vm.IsEditing && e.Key == Key.Enter)
            {
                var modifiers = e.KeyModifiers;
                bool isCtrlOrMeta = (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;

                if (isCtrlOrMeta)
                {
                    if (vm.SaveEditCommand.CanExecute(null))
                    {
                        vm.SaveEditCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // ZOOM SHORTCUTS: Only if image preview is open
            if (vm.IsImagePreviewOpen)
            {
                var modifiers = e.KeyModifiers;
                bool isCtrlOrMeta = (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;

                if (isCtrlOrMeta)
                {
                    // Reset: Ctrl+0
                    if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                    {
                        CalculateInitialZoom(vm);
                        e.Handled = true;
                    }
                    // Zoom In: Ctrl + Plus
                    else if (e.Key == Key.OemPlus || e.Key == Key.Add)
                    {
                        vm.ZoomInCommand.Execute(null);
                        e.Handled = true;
                    }
                    // Zoom Out: Ctrl + Minus
                    else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                    {
                        vm.ZoomOutCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }

            // Arrow Navigation: Only if preview is open AND not editing AND image preview is closed AND not confirming delete
            if (vm.IsPreviewOpen && !vm.IsEditing && !vm.IsImagePreviewOpen && !vm.IsConfirmingDelete)
            {
                if (e.Key == Key.Right)
                {
                    if (vm.NavigateNextPreviewCommand.CanExecute(null))
                    {
                        vm.NavigateNextPreviewCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Left)
                {
                    if (vm.NavigatePreviousPreviewCommand.CanExecute(null))
                    {
                        vm.NavigatePreviousPreviewCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }

        private void TopLevelOnKeyDownBubble(object? sender, KeyEventArgs e)
        {
            if (e.Handled || !IsEffectivelyVisible || DataContext is not CardListViewModel vm) return;

            if (e.Key == Key.Escape)
            {
                // Only go back if NO overlay is open
                if (!vm.IsImagePreviewOpen && !vm.IsPreviewOpen && !vm.IsConfirmingDelete)
                {
                    if (vm.GoBackCommand.CanExecute(null))
                    {
                        vm.GoBackCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_boundViewModel != null)
            {
                _boundViewModel.ShowSaveFileDialog -= HandleShowSaveDialogAsync;
            }

            if (DataContext is CardListViewModel vm)
            {
                vm.ShowSaveFileDialog += HandleShowSaveDialogAsync;
                _boundViewModel = vm;
            }
            else
            {
                _boundViewModel = null;
            }
        }

        private async Task<Stream?> HandleShowSaveDialogAsync(string suggestedName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return null;
            }

            var pdfType = new FilePickerFileType("PDF Dokument")
            {
                Patterns = new[] { "*.pdf" },
                MimeTypes = new[] { "application/pdf" },
                AppleUniformTypeIdentifiers = new[] { "com.adobe.pdf" }
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "PDF speichern unter...",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[] { pdfType },
                DefaultExtension = "pdf"
            });

            if (file != null)
            {
                return await file.OpenWriteAsync();
            }
            
            return null;
        }

        private Control? FindParentListControl(Control? control)
        {
            while (control != null)
            {
                if (control is ItemsControl ic && ic is not ListBox) return ic;
                if (control is DataGrid dg) return dg;
                control = control.Parent as Control;
            }
            return null;
        }

        private CardItemViewModel? HitTestCardItem(Control listControl, Point position)
        {
            var control = listControl.InputHitTest(position) as Control;
            while (control != null)
            {
                if (control.DataContext is CardItemViewModel item)
                {
                    return item;
                }
                if (control == listControl) return null;
                control = control.Parent as Control;
            }
            return null;
        }

        private IEnumerable<CardItemViewModel>? GetItemsSource(Control? listControl)
        {
            if (listControl is ItemsControl ic) return ic.ItemsSource as IEnumerable<CardItemViewModel>;
            if (listControl is DataGrid dg) return dg.ItemsSource as IEnumerable<CardItemViewModel>;
            return null;
        }

        private void StartPointerSelection(Control listControl, CardItemViewModel item, PointerPressedEventArgs e)
        {
            var itemsSource = GetItemsSource(listControl)?.ToList();
            if (itemsSource == null) return;

            _activeListControl = listControl;
            _isPointerSelecting = true;
            _anchorIndex = itemsSource.IndexOf(item);
            
            if (_anchorIndex < 0)
            {
                _isPointerSelecting = false;
                return;
            }

            _originalSelection = itemsSource.Select(c => c.IsSelected).ToList();
            _selectionTargetState = !_originalSelection[_anchorIndex];
            
            ApplyRangeSelection(itemsSource, _anchorIndex);

            _activeListControl.Focus();
            e.Pointer.Capture(_activeListControl);
            e.Handled = true;
        }

        private void DataGrid_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                var point = e.GetCurrentPoint(this);
                if (point.Properties.IsLeftButtonPressed)
                {
                    // Find the row that was clicked
                    var visual = dg.InputHitTest(e.GetPosition(dg)) as Visual;
                    var row = visual?.FindAncestorOfType<DataGridRow>();
                    var item = row?.DataContext as CardItemViewModel;
                    
                    if (item != null)
                    {
                        // Start drag selection logic
                        StartPointerSelection(dg, item, e);
                    }
                }
            }
        }

        private void CardTile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is CardItemViewModel item)
            {
                var point = e.GetCurrentPoint(this);
                if (point.Properties.IsLeftButtonPressed)
                {
                    var listControl = FindParentListControl(control);
                    if (listControl != null)
                    {
                        StartPointerSelection(listControl, item, e);
                    }
                }
            }
        }

        private void Cards_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPointerSelecting)
            {
                if (_activeListControl != null)
                {
                    var position = e.GetPosition(_activeListControl);
                    var item = HitTestCardItem(_activeListControl, position);
                    var itemsSource = GetItemsSource(_activeListControl)?.ToList();

                    if (item != null && itemsSource != null)
                    {
                        var index = itemsSource.IndexOf(item);
                        if (index >= 0)
                        {
                            ApplyRangeSelection(itemsSource, index);
                        }
                    }
                }

                _isPointerSelecting = false;
                if (e.Pointer.Captured == _activeListControl)
                {
                    e.Pointer.Capture(null);
                }

                _originalSelection = null;
                _anchorIndex = -1;
                _activeListControl = null;
            }
        }

        private void Cards_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPointerSelecting || _activeListControl == null)
            {
                return;
            }

            if (sender != _activeListControl) return;

            var position = e.GetPosition(_activeListControl);
            var item = HitTestCardItem(_activeListControl, position);
            var itemsSource = GetItemsSource(_activeListControl)?.ToList();

            if (item != null && itemsSource != null)
            {
                var index = itemsSource.IndexOf(item);
                if (index >= 0)
                {
                    ApplyRangeSelection(itemsSource, index);
                }
            }
        }

        private void Cards_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isPointerSelecting = false;
            _originalSelection = null;
            _anchorIndex = -1;
            _activeListControl = null;
        }

        private void ApplyRangeSelection(IList<CardItemViewModel> cards, int currentIndex)
        {
            if (_originalSelection == null || _anchorIndex < 0)
            {
                return;
            }

            var min = Math.Min(_anchorIndex, currentIndex);
            var max = Math.Max(_anchorIndex, currentIndex);

            for (int i = 0; i < cards.Count; i++)
            {
                bool original = i < _originalSelection.Count ? _originalSelection[i] : false;
                if (i >= min && i <= max)
                {
                    cards[i].IsSelected = _selectionTargetState;
                }
                else
                {
                    cards[i].IsSelected = original;
                }
            }
        }
    }
}
