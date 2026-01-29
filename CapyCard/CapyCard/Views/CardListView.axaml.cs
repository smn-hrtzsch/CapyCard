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
        private ListBox? _activeListBox; // The ListBox where selection started

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
                    var listBox = FindParentListBox(control);
                    if (listBox == null) return;

                    _activeListBox = listBox;

                    var itemsSource = _activeListBox.ItemsSource as IList<CardItemViewModel>;
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

                    _activeListBox.Focus();
                    e.Pointer.Capture(_activeListBox);
                    e.Handled = true;
                }
            }
        }

        private void CardsListBox_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPointerSelecting)
            {
                if (_activeListBox != null)
                {
                    var position = e.GetPosition(_activeListBox);
                    var item = HitTestCardItem(_activeListBox, position);
                    var itemsSource = _activeListBox.ItemsSource as IList<CardItemViewModel>;

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
                if (e.Pointer.Captured == _activeListBox)
                {
                    e.Pointer.Capture(null);
                }

                _originalSelection = null;
                _anchorIndex = -1;
                _activeListBox = null;
            }
        }

        private void CardsListBox_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPointerSelecting || _activeListBox == null)
            {
                return;
            }

            if (sender != _activeListBox) return;

            var position = e.GetPosition(_activeListBox);
            var item = HitTestCardItem(_activeListBox, position);
            var itemsSource = _activeListBox.ItemsSource as IList<CardItemViewModel>;

            if (item != null && itemsSource != null)
            {
                var index = itemsSource.IndexOf(item);
                if (index >= 0)
                {
                    ApplyRangeSelection(itemsSource, index);
                }
            }
        }

        private void CardsListBox_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isPointerSelecting = false;
            _originalSelection = null;
            _anchorIndex = -1;
            _activeListBox = null;
        }

        private ListBox? FindParentListBox(Control? control)
        {
            while (control != null)
            {
                if (control is ListBox lb) return lb;
                control = control.Parent as Control;
            }
            return null;
        }

        private CardItemViewModel? HitTestCardItem(ListBox listBox, Point position)
        {
            var control = listBox.InputHitTest(position) as Control;
            while (control != null)
            {
                if (control.DataContext is CardItemViewModel item)
                {
                    return item;
                }
                if (control == listBox) return null;
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
