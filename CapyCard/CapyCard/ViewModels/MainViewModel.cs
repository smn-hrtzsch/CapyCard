using CommunityToolkit.Mvvm.ComponentModel;
using CapyCard.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapyCard.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        private readonly Stack<ObservableObject> _navigationStack = new();

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
            _deckDetailViewModel.OnNavigateBack += GoBack;
            _deckDetailViewModel.OnNavigateToCardList += NavigateToCardList;
            _deckDetailViewModel.OnNavigateToLearn += NavigateToLearn;
            _deckDetailViewModel.OnNavigateToDeck += NavigateToDeckDetail;
            _deckDetailViewModel.OnCardCountUpdated += UpdateDeckCardCount;
            _deckDetailViewModel.OnSubDeckAdded += RefreshDeckList;
            
            _cardListViewModel.OnNavigateBack += GoBack;
            _learnViewModel.OnNavigateBack += GoBack;
            
            // NEU: Abonniert das "Bearbeiten"-Event aus der Kartenliste
            _cardListViewModel.OnEditCardRequest += NavigateToEditCard;

            _currentViewModel = _deckListViewModel;
        }

        partial void OnCurrentViewModelChanged(ObservableObject value)
        {
            Console.WriteLine($"[CapyCard.Nav] CurrentViewModel -> {value?.GetType().FullName ?? "<null>"}");
        }

        private void NavigateTo(ObservableObject target, bool pushCurrent)
        {
            if (pushCurrent && CurrentViewModel != target)
            {
                _navigationStack.Push(CurrentViewModel);
            }

            CurrentViewModel = target;
        }

        private async Task ActivateViewModelAsync(ObservableObject vm)
        {
            switch (vm)
            {
                case DeckListViewModel:
                    _deckListViewModel.RefreshDecks();
                    break;
                case DeckDetailViewModel:
                    await _deckDetailViewModel.RefreshCardDataAsync();
                    break;
            }
        }

        private async void GoBack()
        {
            if (_navigationStack.TryPop(out var previous))
            {
                await ActivateViewModelAsync(previous);
                CurrentViewModel = previous;
                return;
            }

            // Fallback: if stack is empty, go to root.
            if (CurrentViewModel != _deckListViewModel)
            {
                _deckListViewModel.RefreshDecks();
                CurrentViewModel = _deckListViewModel;
            }
        }

        private async void NavigateToDeckDetail(Deck selectedDeck)
        {
            await _deckDetailViewModel.LoadDeck(selectedDeck); 
            NavigateTo(_deckDetailViewModel, pushCurrent: CurrentViewModel != _deckDetailViewModel);
        }
        
        private void NavigateToCardList(Deck deck)
        {
            _cardListViewModel.LoadDeck(deck);

            // If we're leaving DeckDetail while editing (CancelEdit path), treat it like a back-navigation.
            bool isCancelEditPath = CurrentViewModel is DeckDetailViewModel detail && detail.IsEditing;
            NavigateTo(_cardListViewModel, pushCurrent: !isCancelEditPath);
        }
        
        private async void NavigateToLearn(Deck deck, LearningMode mode, List<int>? selectedIds)
        {
            try
            {
                await _learnViewModel.LoadSession(deck, mode, selectedIds);
                NavigateTo(_learnViewModel, pushCurrent: CurrentViewModel != _learnViewModel);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to learn view: {ex}");
                // Optional: Show error message to user if possible, or just stay on current view
            }
        }

        private void UpdateDeckCardCount(Deck deck, int count)
        {
            _deckListViewModel.UpdateDeckCardCount(deck.Id, count);
        }

        private void RefreshDeckList()
        {
            _deckListViewModel.RefreshDecks();
        }

        // NEU: Diese Methode wird vom 'OnEditCardRequest'-Event aufgerufen
        // KORREKTUR: 'async' hinzugefügt
        private async void NavigateToEditCard(Deck deck, Card card)
        {
            // Ruft die neue Methode auf, die wir in Teil 1 erstellt haben
            // KORREKTUR: 'await' hinzugefügt
            await _deckDetailViewModel.LoadCardForEditing(deck, card);
            
            // Zeigt die Detail-Ansicht an (jetzt im "Bearbeiten"-Modus)
            NavigateTo(_deckDetailViewModel, pushCurrent: CurrentViewModel != _deckDetailViewModel);
        }

        public bool HandleHardwareBack()
        {
            if (CurrentViewModel is LearnViewModel learnVm)
            {
                // Learning Mode: first close transient overlays/states, then navigate back.
                if (learnVm.IsImagePreviewOpen && learnVm.CloseImagePreviewCommand.CanExecute(null))
                {
                    learnVm.CloseImagePreviewCommand.Execute(null);
                    return true;
                }

                if (learnVm.IsEditing && learnVm.CancelEditCommand.CanExecute(null))
                {
                    learnVm.CancelEditCommand.Execute(null);
                    return true;
                }

                GoBack();
                return true;
            }
            else if (CurrentViewModel is DeckDetailViewModel detailVm)
            {
                // Prefer app navigation history (previous screen) if available.
                if (_navigationStack.Count > 0)
                {
                    GoBack();
                    return true;
                }

                // Otherwise, allow DeckDetail to handle hierarchical back (subdeck -> parent deck).
                if (detailVm.GoBackCommand.CanExecute(null))
                {
                    detailVm.GoBackCommand.Execute(null);
                    return true;
                }
            }

            if (_navigationStack.Count > 0)
            {
                GoBack();
                return true;
            }
            
            // If in DeckListView, return false (let system exit)
            return false;
        }
    }
}