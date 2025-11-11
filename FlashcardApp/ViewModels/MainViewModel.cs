using CommunityToolkit.Mvvm.ComponentModel;
using FlashcardApp.Models; // <-- HIER IST DIE KORREKTUR

namespace FlashcardApp.ViewModels
{
    // Das ist der neue DataContext für unser MainWindow.
    // Er ist ein ObservableObject, damit die UI seine Änderungen mitbekommt.
    public partial class MainViewModel : ObservableObject
    {
        // [ObservableProperty] erstellt eine Eigenschaft "CurrentViewModel".
        // Wenn sich diese ändert, ändert sich die Ansicht im MainWindow.
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        // Wir erstellen die beiden "Seiten" (ViewModels)
        private readonly DeckListViewModel _deckListViewModel;
        private readonly DeckDetailViewModel _deckDetailViewModel;

        public MainViewModel()
        {
            // Wir erstellen Instanzen unserer "Seiten"
            _deckListViewModel = new DeckListViewModel();
            _deckDetailViewModel = new DeckDetailViewModel();

            // Wir verknüpfen die Navigation
            // Wenn im DeckListViewModel ein Deck ausgewählt wird...
            _deckListViewModel.OnDeckSelected += NavigateToDeckDetail;
            // Wenn im DeckDetailViewModel "Zurück" geklickt wird...
            _deckDetailViewModel.OnNavigateBack += NavigateToDeckList;

            // Startseite festlegen
            _currentViewModel = _deckListViewModel;
        }

        // Diese Methode wird aufgerufen, um zur Karten-Seite zu wechseln
        private void NavigateToDeckDetail(Deck selectedDeck)
        {
            _deckDetailViewModel.LoadDeck(selectedDeck); // Deck laden
            CurrentViewModel = _deckDetailViewModel;     // Ansicht wechseln
        }

        // Diese Methode wird aufgerufen, um zur Fächer-Liste zurückzukehren
        private void NavigateToDeckList()
        {
            CurrentViewModel = _deckListViewModel; // Ansicht wechseln
        }
    }
}