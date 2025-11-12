using CommunityToolkit.Mvvm.ComponentModel;
using FlashcardApp.Models;
using System.Threading.Tasks; // NEU

namespace FlashcardApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        private readonly DeckListViewModel _deckListViewModel;
        private readonly DeckDetailViewModel _deckDetailViewModel;
        private readonly CardListViewModel _cardListViewModel;

        public MainViewModel()
        {
            _deckListViewModel = new DeckListViewModel();
            _deckDetailViewModel = new DeckDetailViewModel();
            _cardListViewModel = new CardListViewModel(); 

            _deckListViewModel.OnDeckSelected += NavigateToDeckDetail;
            _deckDetailViewModel.OnNavigateBack += NavigateToDeckList;
            
            // NEU: Signatur angepasst
            _deckDetailViewModel.OnNavigateToCardList += NavigateToCardList;
            _cardListViewModel.OnNavigateBack += NavigateBackToDeckDetail;

            _currentViewModel = _deckListViewModel;
        }

        // NEU: 'async' und 'void'
        private async void NavigateToDeckDetail(Deck selectedDeck)
        {
            // NEU: 'await' hinzugefügt, da LoadDeck jetzt Task ist
            await _deckDetailViewModel.LoadDeck(selectedDeck); 
            CurrentViewModel = _deckDetailViewModel;     
        }

        private void NavigateToDeckList()
        {
            CurrentViewModel = _deckListViewModel; 
        }
        
        // NEU: Signatur angepasst. Nimmt nur noch Deck.
        private void NavigateToCardList(Deck deck)
        {
            // NEU: LoadDeck hat neue Signatur
            _cardListViewModel.LoadDeck(deck);
            CurrentViewModel = _cardListViewModel;
        }
        
        // NEU: 'async' und 'void'
        private async void NavigateBackToDeckDetail()
        {
            // NEU: Ruft die Refresh-Methode auf, um den Kartenzähler zu aktualisieren
            await _deckDetailViewModel.RefreshCardCountAsync();
            CurrentViewModel = _deckDetailViewModel;
        }
    }
}