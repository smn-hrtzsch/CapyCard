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

namespace CapyCard.Views
{
    public partial class CardListView : UserControl
    {
        private CardListViewModel? _boundViewModel;
        private TopLevel? _topLevel;
        
        // Selection state
        private bool _isPointerSelecting;
        private bool _selectionTargetState;
        private int _anchorIndex = -1;
        private List<bool>? _originalSelection;
        private ItemsControl? _activeItemsControl; // The ItemsControl where selection started

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
            if (e.Handled || !IsEffectivelyVisible || DataContext is not CardListViewModel vm) return;

            if (e.Key == Key.Escape)
            {
                if (vm.GoBackCommand.CanExecute(null))
                {
                    vm.GoBackCommand.Execute(null);
                    e.Handled = true;
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

        private void CardTile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is CardItemViewModel item)
            {
                var point = e.GetCurrentPoint(this);
                if (point.Properties.IsLeftButtonPressed)
                {
                    var itemsControl = FindParentItemsControl(control);
                    if (itemsControl == null) return;

                    _activeItemsControl = itemsControl;

                    var itemsSource = _activeItemsControl.ItemsSource as IList<CardItemViewModel>;
                    if (itemsSource == null) return;

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

                    _activeItemsControl.Focus();
                    e.Pointer.Capture(_activeItemsControl);
                    e.Handled = true;
                }
            }
        }

        private void Cards_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPointerSelecting)
            {
                if (_activeItemsControl != null)
                {
                    var position = e.GetPosition(_activeItemsControl);
                    var item = HitTestCardItem(_activeItemsControl, position);
                    var itemsSource = _activeItemsControl.ItemsSource as IList<CardItemViewModel>;

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
                if (e.Pointer.Captured == _activeItemsControl)
                {
                    e.Pointer.Capture(null);
                }

                _originalSelection = null;
                _anchorIndex = -1;
                _activeItemsControl = null;
            }
        }

        private void Cards_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPointerSelecting || _activeItemsControl == null)
            {
                return;
            }

            if (sender != _activeItemsControl) return;

            var position = e.GetPosition(_activeItemsControl);
            var item = HitTestCardItem(_activeItemsControl, position);
            var itemsSource = _activeItemsControl.ItemsSource as IList<CardItemViewModel>;

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
            _activeItemsControl = null;
        }

        private ItemsControl? FindParentItemsControl(Control? control)
        {
            while (control != null)
            {
                if (control is ItemsControl ic && ic is not ListBox) return ic;
                // Note: We check 'is not ListBox' because ListBox inherits from ItemsControl
                // and we want specifically our outer ItemsControl if we were using nested ones,
                // but here we just want the one that is NOT a ListBox (since we removed ListBox).
                // Actually, just 'is ItemsControl' is fine now.
                if (control is ItemsControl ic2) return ic2;
                control = control.Parent as Control;
            }
            return null;
        }

        private CardItemViewModel? HitTestCardItem(ItemsControl itemsControl, Point position)
        {
            var control = itemsControl.InputHitTest(position) as Control;
            while (control != null)
            {
                if (control.DataContext is CardItemViewModel item)
                {
                    return item;
                }
                if (control == itemsControl) return null;
                control = control.Parent as Control;
            }
            return null;
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
