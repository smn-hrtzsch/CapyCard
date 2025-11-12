using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Models;
using System;

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

        public DeckItemViewModel(Deck deck)
        {
            Deck = deck;
            _name = deck.Name;
            _editText = deck.Name;
            _isEditing = false;
        }

        // KORREKTUR: Von 'private' zu 'public' geändert
        [RelayCommand]
        public void StartEdit()
        {
            EditText = Name; // Synchronisiere Text beim Start
            IsEditing = true;
        }

        // KORREKTUR: Von 'private' zu 'public' geändert
        // Dies behebt den CS0122 Build-Fehler.
        [RelayCommand]
        public void CancelEdit()
        {
            IsEditing = false;
        }
    }
}