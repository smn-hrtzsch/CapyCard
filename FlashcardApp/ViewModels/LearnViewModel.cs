using CommunityToolkit.Mvvm.ComponentModel; // <-- KORREKTUR: Mvvm (zwei 'v')
using CommunityToolkit.Mvvm.Input;       // <-- KORREKTUR: Mvvm (zwei 'v')
using FlashcardApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices; // Für CollectionsMarshal

namespace FlashcardApp.ViewModels
{
    public partial class LearnViewModel : ObservableObject
    {
        // === Eigenschaften für die UI ===

        [ObservableProperty]
        private string _currentCardFront = string.Empty;

        [ObservableProperty]
        private string _currentCardBack = string.Empty;

        [ObservableProperty]
        private bool _isBackVisible = false;

        [ObservableProperty]
        private bool _isDeckFinished = false;

        [ObservableProperty]
        private bool _showShowBackButton = false;

        [ObservableProperty]
        private bool _showNextCardButton = false;

        [ObservableProperty]
        private bool _showReshuffleButton = false;


        // === Interne Logik ===
        private List<Card> _allCards = new();
        private Queue<Card> _shuffledDeck = new();
        public event Action? OnNavigateBack;

        
        public void LoadDeck(List<Card> cards)
        {
            _allCards = cards;
            Reshuffle(); 
        }

        private void Reshuffle()
        {
            if (_allCards.Count == 0)
            {
                IsDeckFinished = true;
                CurrentCardFront = "Keine Karten in diesem Fach.";
                CurrentCardBack = string.Empty;
                IsBackVisible = false;

                ShowShowBackButton = false;
                ShowNextCardButton = false;
                ShowReshuffleButton = false; 
                return;
            }

            var shuffledList = _allCards.ToList(); 
            Random.Shared.Shuffle(CollectionsMarshal.AsSpan(shuffledList));
            
            _shuffledDeck = new Queue<Card>(shuffledList);
            ShowNextCard(); 
        }

        private void ShowNextCard()
        {
            if (_shuffledDeck.Count > 0)
            {
                var card = _shuffledDeck.Dequeue();
                CurrentCardFront = card.Front;
                CurrentCardBack = card.Back;
                IsBackVisible = false; 
                IsDeckFinished = false;

                ShowShowBackButton = true;
                ShowNextCardButton = false;
                ShowReshuffleButton = false;
            }
            else
            {
                CurrentCardFront = "Deck beendet!";
                CurrentCardBack = "Alle Karten gelernt. Nochmal mischen?";
                IsBackVisible = true; 
                IsDeckFinished = true; 

                ShowShowBackButton = false;
                ShowNextCardButton = false;
                ShowReshuffleButton = true;
            }
        }

        [RelayCommand]
        private void ShowBack()
        {
            IsBackVisible = true;

            ShowShowBackButton = false;
            ShowNextCardButton = true;
            ShowReshuffleButton = false;
        }

        [RelayCommand]
        private void NextCard()
        {
            if (IsDeckFinished)
            {
                Reshuffle(); 
            }
            else
            {
                ShowNextCard(); 
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
    }
}