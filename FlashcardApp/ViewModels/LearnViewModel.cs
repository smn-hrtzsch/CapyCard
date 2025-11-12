using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FlashcardApp.ViewModels
{
    public partial class LearnViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;

        // === Eigenschaften für die UI ===
        [ObservableProperty]
        private string _currentCardFront = string.Empty;

        [ObservableProperty]
        private string _currentCardBack = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEditButton))]
        private bool _isBackVisible = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditing))]
        [NotifyPropertyChangedFor(nameof(ShowEditButton))]
        private bool _isDeckFinished = false;

        // === KORRIGIERTE Eigenschaften für den Bearbeiten-Zustand ===
        
        // Steuert, ob WIR GERADE bearbeiten (nur noch eine Eigenschaft)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEditButton))]
        private bool _isEditing = false;
        
        // (IsEditingFront und IsEditingBack wurden entfernt)
        
        // Temporärer Speicher für den Editor
        [ObservableProperty]
        private string _editFrontText = string.Empty;
        
        [ObservableProperty]
        private string _editBackText = string.Empty;


        // === Button-Sichtbarkeit ===
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditing))]
        private bool _showShowBackButton = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditing))]
        private bool _showNextCardButton = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditing))]
        private bool _showReshuffleButton = false;

        // === Interne Logik ===
        private List<Card> _allCards = new();
        private Queue<Card> _shuffledDeck = new();
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEditButton))]
        private Card? _currentCard;

        public event Action? OnNavigateBack;

        public bool ShowEditButton => IsBackVisible && !IsEditing && CurrentCard != null;
        
        public LearnViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public void LoadDeck(List<Card> cards)
        {
            _allCards = cards;
            Reshuffle(); 
        }

        private void Reshuffle()
        {
            IsEditing = false; 
            if (_allCards.Count == 0)
            {
                // (Rest unverändert)
                IsDeckFinished = true;
                CurrentCardFront = "Keine Karten in diesem Fach.";
                CurrentCardBack = string.Empty;
                CurrentCard = null;
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
            IsEditing = false; 
            if (_shuffledDeck.Count > 0)
            {
                CurrentCard = _shuffledDeck.Dequeue();
                CurrentCardFront = CurrentCard.Front;
                CurrentCardBack = CurrentCard.Back;
                IsBackVisible = false; 
                IsDeckFinished = false;

                ShowShowBackButton = true;
                ShowNextCardButton = false;
                ShowReshuffleButton = false;
            }
            else
            {
                // (Rest unverändert)
                CurrentCardFront = "Deck beendet!";
                CurrentCardBack = "Alle Karten gelernt. Nochmal mischen?";
                CurrentCard = null; 
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

        // === KORRIGIERTE Befehle für Inline-Bearbeitung ===

        // KORRIGIERT: 'part'-Parameter entfernt.
        [RelayCommand]
        private void StartEdit()
        {
            if (CurrentCard == null) return;
            
            IsEditing = true;
            
            // KORRIGIERT: Lade BEIDE Texte in die Editor-Felder
            EditFrontText = CurrentCardFront;
            EditBackText = CurrentCardBack;
            
            // Verstecke die Lern-Buttons
            ShowShowBackButton = false;
            ShowNextCardButton = false;
            ShowReshuffleButton = false;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            // Stelle die korrekten Lern-Buttons wieder her
            if (IsDeckFinished)
            {
                ShowReshuffleButton = true;
            }
            else
            {
                ShowNextCardButton = true;
            }
        }

        [RelayCommand]
        private async Task SaveEdit()
        {
            if (CurrentCard == null)
            {
                CancelEdit();
                return;
            }

            var trackedCard = await _dbContext.Cards.FindAsync(CurrentCard.Id);
            if (trackedCard != null)
            {
                // KORRIGIERT: Aktualisiere BEIDE Seiten
                trackedCard.Front = EditFrontText;
                trackedCard.Back = EditBackText;
                
                // Aktualisiere die UI-Ansicht
                CurrentCardFront = EditFrontText;
                CurrentCardBack = EditBackText;
                
                // Aktualisiere die 'Master'-Liste für den nächsten Shuffle
                CurrentCard.Front = EditFrontText;
                CurrentCard.Back = EditBackText;

                await _dbContext.SaveChangesAsync();
            }
            
            // Beende den Bearbeiten-Modus
            CancelEdit(); 
        }
    }
}