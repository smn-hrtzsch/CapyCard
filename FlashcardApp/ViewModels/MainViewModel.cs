using CommunityToolkit.Mvvm.ComponentModel;
using FlashcardApp.Models;
using System.Collections.Generic; // NEU
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
        
        // NEU: Eine Instanz für den Lern-Modus
        private readonly LearnViewModel _learnViewModel;

        public MainViewModel()
        {
            _deckListViewModel = new DeckListViewModel();
            _deckDetailViewModel = new DeckDetailViewModel();
            _cardListViewModel = new CardListViewModel(); 
            _learnViewModel = new LearnViewModel(); // NEU: Instanziieren

            // Navigation verknüpfen
            _deckListViewModel.OnDeckSelected += NavigateToDeckDetail;
            _deckDetailViewModel.OnNavigateBack += NavigateToDeckList;
            _deckDetailViewModel.OnNavigateToCardList += NavigateToCardList;
            _cardListViewModel.OnNavigateBack += NavigateBackToDeckDetail;
            
            // NEUE Navigations-Pfade
            _deckDetailViewModel.OnNavigateToLearn += NavigateToLearn;
            _learnViewModel.OnNavigateBack += NavigateBackToDeckDetail;

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
        
        // NEU: 'async' entfernt, da Refresh jetzt in LoadDeck passiert
        private void NavigateBackToDeckDetail()
        {
            // Beim Zurückkehren einfach die Ansicht wechseln.
            // Die Daten (Kartenzahl) im DeckDetailViewModel sind bereits aktuell,
            // da sie bei JEDER Aktion (Add, Delete) aktualisiert wurden.
            // Oh, Moment. RefreshCardDataAsync() wird in CardListViewModel nicht aufgerufen.
            // Wir müssen es hier tun.
            RefreshDeckDetailData();
            CurrentViewModel = _deckDetailViewModel;
        }
        
        // NEU: Asynchrone Hilfsmethode
        private async void RefreshDeckDetailData()
        {
            await _deckDetailViewModel.RefreshCardDataAsync();
        }
        
        // NEU: Navigation zum Lern-Modus
        private void NavigateToLearn(List<Card> cards)
        {
            _learnViewModel.LoadDeck(cards);
            CurrentViewModel = _learnViewModel;
        }
    }
}