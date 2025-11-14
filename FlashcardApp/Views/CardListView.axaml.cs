using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using FlashcardApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlashcardApp.Views
{
    public partial class CardListView : UserControl
    {
        private CardListViewModel? _boundViewModel;
        private bool _isPointerSelecting;
        private bool _selectionTargetState;
        private int _anchorIndex = -1;
        private List<bool>? _originalSelection;

        public CardListView()
        {
            InitializeComponent();
            
            this.DataContextChanged += OnDataContextChanged;
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
        /// </summary>
        private async Task<string?> HandleShowSaveDialogAsync(string suggestedName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return null;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "PDF speichern unter...",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[] { FilePickerFileTypes.Pdf } 
            });

            // --- HIER IST DIE KORREKTUR ---
            // Die Methode 'TryGetUri' existiert nicht auf IStorageFile.
            // Die korrekte Methode ist 'TryGetLocalPath()', die direkt einen string? zurückgibt.
            return file?.TryGetLocalPath();
        }

        private void CardTile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is CardItemViewModel item)
            {
                var point = e.GetCurrentPoint(this);
                if (point.Properties.IsLeftButtonPressed)
                {
                    if (e.ClickCount >= 2)
                    {
                        item.IsSelected = !item.IsSelected;
                        CardsListBox?.Focus();
                        e.Handled = true;
                        return;
                    }

                    if (_boundViewModel?.Cards == null)
                    {
                        return;
                    }

                    _isPointerSelecting = true;
                    _anchorIndex = _boundViewModel.Cards.IndexOf(item);
                    if (_anchorIndex < 0)
                    {
                        _isPointerSelecting = false;
                        return;
                    }

                    _originalSelection = _boundViewModel.Cards.Select(c => c.IsSelected).ToList();
                    _selectionTargetState = !_originalSelection[_anchorIndex];
                    ApplyRangeSelection(_anchorIndex);

                    CardsListBox?.Focus();
                    e.Pointer.Capture(CardsListBox);
                    e.Handled = true;
                }
            }
        }

        private void CardsListBox_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPointerSelecting)
            {
                if (CardsListBox != null && _boundViewModel?.Cards != null)
                {
                    var position = e.GetPosition(CardsListBox);
                    var item = HitTestCardItem(position);
                    if (item != null)
                    {
                        var index = _boundViewModel.Cards.IndexOf(item);
                        if (index >= 0)
                        {
                            ApplyRangeSelection(index);
                        }
                    }
                }

                _isPointerSelecting = false;
                if (e.Pointer.Captured == CardsListBox)
                {
                    e.Pointer.Capture(null);
                }

                _originalSelection = null;
                _anchorIndex = -1;
            }
        }

        private void CardsListBox_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPointerSelecting || CardsListBox == null)
            {
                return;
            }

            var position = e.GetPosition(CardsListBox);
            var item = HitTestCardItem(position);
            if (item != null && _boundViewModel?.Cards != null)
            {
                var index = _boundViewModel.Cards.IndexOf(item);
                if (index >= 0)
                {
                    ApplyRangeSelection(index);
                }
            }
        }

        private void CardsListBox_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isPointerSelecting = false;
            _originalSelection = null;
            _anchorIndex = -1;
        }

        private CardItemViewModel? HitTestCardItem(Point position)
        {
            if (CardsListBox == null)
            {
                return null;
            }

            var control = CardsListBox.InputHitTest(position) as Control;
            while (control != null)
            {
                if (control.DataContext is CardItemViewModel item)
                {
                    return item;
                }

                control = control.Parent as Control;
            }

            return null;
        }

        private void ApplyRangeSelection(int currentIndex)
        {
            if (_boundViewModel?.Cards == null || _originalSelection == null || _anchorIndex < 0)
            {
                return;
            }

            var cards = _boundViewModel.Cards;
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