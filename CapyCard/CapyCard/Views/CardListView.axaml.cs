using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
            IsCompactMode = e.NewSize.Width < 800;
        }

        /// <summary>
        /// Wird aufgerufen, wenn das ViewModel (DataContext) gesetzt wird.
        /// Wir "abonnieren" hier das Dialog-Event des ViewModels.
        /// </summary>
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

        /// <summary>
        /// Diese Methode wird vom ViewModel aufgerufen. Sie öffnet den nativen "Speichern unter"-Dialog.
        /// Gibt einen Schreib-Stream zurück, in den QuestPDF schreiben kann.
        /// </summary>
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
                // WICHTIG: Wir öffnen hier den Stream. Der Aufrufer (ViewModel/Service) muss ihn schließen!
                return await file.OpenWriteAsync();
            }
            
            return null;
        }

        // Attached to the Border (Card Tile) inside the ListBox
        private void CardTile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is CardItemViewModel item)
            {
                var point = e.GetCurrentPoint(this);
                if (point.Properties.IsLeftButtonPressed)
                {
                    // Find the parent ListBox
                    var listBox = FindParentListBox(control);
                    if (listBox == null) return;

                    _activeListBox = listBox;

                    // Double click logic
                    // if (e.ClickCount >= 2)
                    // {
                    //    item.IsSelected = !item.IsSelected;
                    //    _activeListBox.Focus();
                    //    e.Handled = true;
                    //    return;
                    // }

                    // Start drag selection
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
                    
                    // Toggle the clicked item immediately (Click to toggle behavior)
                    // If it was selected, target state is unselected. If unselected, target is selected.
                    _selectionTargetState = !_originalSelection[_anchorIndex];
                    
                    ApplyRangeSelection(itemsSource, _anchorIndex);

                    _activeListBox.Focus();
                    e.Pointer.Capture(_activeListBox);
                    e.Handled = true;
                }
            }
        }

        // Attached to the ListBox itself
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

        // Attached to the ListBox itself
        private void CardsListBox_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPointerSelecting || _activeListBox == null)
            {
                return;
            }

            // Ensure we are moving over the active listbox
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

        // Attached to the ListBox itself
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
                if (control == listBox) return null; // Don't go up past the listbox
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