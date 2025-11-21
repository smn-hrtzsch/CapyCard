using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Models;
using System;
using System.Collections.ObjectModel;

namespace FlashcardApp.ViewModels
{
    /// <summary>
    /// Ein Wrapper um das Deck-Modell, der UI-Zustände wie IsEditing verwaltet.
    /// </summary>
    public partial class DeckItemViewModel : ObservableObject
    {
        /// <summary>
        /// Das rohe Daten-Modell
        /// </summary>
        public Deck Deck { get; }

        [ObservableProperty]
        private string _name; 

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _editText;

        [ObservableProperty]
        private int _cardCount;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _hasSubDecks;

        public ObservableCollection<DeckItemViewModel> SubDecks { get; } = new();

        public DeckItemViewModel(Deck deck, int cardCount = 0)
        {
            Deck = deck;
            _name = deck.Name;
            _editText = deck.Name;
            _isEditing = false;
            _cardCount = cardCount;
        }

        [RelayCommand]
        public void StartEdit()
        {
            EditText = Name; // Synchronisiere Text beim Start
            IsEditing = true;
        }

        [RelayCommand]
        public void CancelEdit()
        {
            IsEditing = false;
        }

        [RelayCommand]
        public void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}