using CommunityToolkit.Mvvm.ComponentModel;
using FlashcardApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlashcardApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        private readonly DeckListViewModel _deckListViewModel;
        private readonly DeckDetailViewModel _deckDetailViewModel;
        private readonly CardListViewModel _cardListViewModel;
        private readonly LearnViewModel _learnViewModel;

        public MainViewModel()
        {
            _deckListViewModel = new DeckListViewModel();
            _deckDetailViewModel = new DeckDetailViewModel();
            _cardListViewModel = new CardListViewModel(); 
            _learnViewModel = new LearnViewModel(); 

            // Navigation verknüpfen
            _deckListViewModel.OnDeckSelected += NavigateToDeckDetail;
            _deckDetailViewModel.OnNavigateBack += NavigateToDeckList;
            _deckDetailViewModel.OnNavigateToCardList += NavigateToCardList;
            _deckDetailViewModel.OnNavigateToLearn += NavigateToLearn;
            _deckDetailViewModel.OnNavigateToDeck += NavigateToDeckDetail;
            _deckDetailViewModel.OnCardCountUpdated += UpdateDeckCardCount;
            
            _cardListViewModel.OnNavigateBack += NavigateBackToDeckDetail;
            _learnViewModel.OnNavigateBack += NavigateBackToDeckDetail;
            
            // NEU: Abonniert das "Bearbeiten"-Event aus der Kartenliste
            _cardListViewModel.OnEditCardRequest += NavigateToEditCard;

            _currentViewModel = _deckListViewModel;
        }

        private async void NavigateToDeckDetail(Deck selectedDeck)
        {
            await _deckDetailViewModel.LoadDeck(selectedDeck); 
            CurrentViewModel = _deckDetailViewModel;     
        }

        private void NavigateToDeckList()
        {
            CurrentViewModel = _deckListViewModel; 
        }
        
        private void NavigateToCardList(Deck deck)
        {
            _cardListViewModel.LoadDeck(deck);
            CurrentViewModel = _cardListViewModel;
        }
        
        private async void NavigateBackToDeckDetail()
        {
            // Ruft Refresh auf, um Zähler (nach Löschen/Hinzufügen) zu aktualisieren
            await _deckDetailViewModel.RefreshCardDataAsync();
            CurrentViewModel = _deckDetailViewModel;
        }
        
        private async void NavigateToLearn(Deck deck)
        {
            await _learnViewModel.LoadDeck(deck);
            CurrentViewModel = _learnViewModel;
        }

        private void UpdateDeckCardCount(Deck deck, int count)
        {
            _deckListViewModel.UpdateDeckCardCount(deck.Id, count);
        }

        // NEU: Diese Methode wird vom 'OnEditCardRequest'-Event aufgerufen
        // KORREKTUR: 'async' hinzugefügt
        private async void NavigateToEditCard(Deck deck, Card card)
        {
            // Ruft die neue Methode auf, die wir in Teil 1 erstellt haben
            // KORREKTUR: 'await' hinzugefügt
            await _deckDetailViewModel.LoadCardForEditing(deck, card);
            
            // Zeigt die Detail-Ansicht an (jetzt im "Bearbeiten"-Modus)
            CurrentViewModel = _deckDetailViewModel;
        }
    }
}