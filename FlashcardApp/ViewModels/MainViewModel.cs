using CommunityToolkit.Mvvm.ComponentModel;
using FlashcardApp.Models;

namespace FlashcardApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        private readonly DeckListViewModel _deckListViewModel;
        private readonly DeckDetailViewModel _deckDetailViewModel;
        
        // NEU: Eine Instanz für die Karten-Liste
        private readonly CardListViewModel _cardListViewModel;

        public MainViewModel()
        {
            // Instanzen erstellen
            _deckListViewModel = new DeckListViewModel();
            _deckDetailViewModel = new DeckDetailViewModel();
            _cardListViewModel = new CardListViewModel(); // NEU

            // Navigation verknüpfen
            _deckListViewModel.OnDeckSelected += NavigateToDeckDetail;
            _deckDetailViewModel.OnNavigateBack += NavigateToDeckList;
            
            // NEUE Navigations-Pfade
            _deckDetailViewModel.OnNavigateToCardList += NavigateToCardList;
            _cardListViewModel.OnNavigateBack += NavigateBackToDeckDetail;

            // Startseite festlegen
            _currentViewModel = _deckListViewModel;
        }

        // Navigation zur Fach-Detail-Ansicht (Karten hinzufügen)
        private void NavigateToDeckDetail(Deck selectedDeck)
        {
            _deckDetailViewModel.LoadDeck(selectedDeck); 
            CurrentViewModel = _deckDetailViewModel;     
        }

        // Navigation zurück zur Fächer-Liste
        private void NavigateToDeckList()
        {
            CurrentViewModel = _deckListViewModel; 
        }
        
        // NEU: Navigation zur Karten-Liste
        private void NavigateToCardList(Deck deck, System.Collections.ObjectModel.ObservableCollection<Card> cards)
        {
            _cardListViewModel.LoadDeck(deck, cards);
            CurrentViewModel = _cardListViewModel;
        }
        
        // NEU: Navigation zurück zur Fach-Detail-Ansicht
        private void NavigateBackToDeckDetail()
        {
            // Wir müssen nicht neu laden, das ViewModel existiert noch
            CurrentViewModel = _deckDetailViewModel;
        }
    }
}