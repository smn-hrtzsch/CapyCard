using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapyCard.Models;
using CapyCard.Data;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CapyCard.ViewModels
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
        [NotifyPropertyChangedFor(nameof(TopicCountText))]
        private int _cardCount;

        public string TopicCountText
        {
            get
            {
                if (Deck.ParentDeckId != null) return $"{CardCount} Karten";
                return $"{SubDecks.Count} Themen • {CardCount} Karten";
            }
        }

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanExpand))]
        private bool _hasSubDecks;

        public bool CanExpand => HasSubDecks || Deck.ParentDeckId == null;

        [ObservableProperty]
        private bool _isStatic; // Wenn true, darf das Deck nicht bearbeitet oder gelöscht werden (z.B. "Allgemein")

        [ObservableProperty]
        private string _newSubDeckName = string.Empty;

        public ObservableCollection<DeckItemViewModel> SubDecks { get; } = new();

        public DeckItemViewModel(Deck deck, int cardCount = 0)
        {
            Deck = deck;
            _name = deck.Name;
            _editText = deck.Name;
            _isEditing = false;
            _cardCount = cardCount;
            SubDecks.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TopicCountText));
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

        [RelayCommand]
        public async Task AddSubDeck()
        {
            if (string.IsNullOrWhiteSpace(NewSubDeckName)) return;

            using (var context = new FlashcardDbContext())
            {
                var newDeck = new Deck
                {
                    Name = NewSubDeckName,
                    ParentDeckId = this.Deck.Id
                };
                context.Decks.Add(newDeck);
                await context.SaveChangesAsync();

                var newVm = new DeckItemViewModel(newDeck);
                SubDecks.Add(newVm);
                HasSubDecks = true;
            }
            NewSubDeckName = string.Empty;
        }
    }
}