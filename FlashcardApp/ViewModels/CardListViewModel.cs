using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FlashcardApp.ViewModels;
using FlashcardApp.Views;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using FlashcardApp.Models;
using CommunityToolkit.Mvvm.Input;

namespace FlashcardApp.ViewModels
{
    public partial class CardListViewModel : ObservableObject
    {
        private Deck? _currentDeck;

        // Zeigt den Namen des Fachs oben an
        [ObservableProperty]
        private string _deckName = "Karten";

        public ObservableCollection<Card> Cards { get; } = new();

        // Event, um dem MainViewModel zu sagen: "Wir wollen zurück!"
        public event Action? OnNavigateBack;

        public CardListViewModel()
        {
            // Konstruktor (bleibt vorerst leer)
        }

        // Wird vom MainViewModel aufgerufen, um die Karten zu laden
        public void LoadDeck(Deck deck, ObservableCollection<Card> cards)
        {
            _currentDeck = deck;
            DeckName = $"Karten für: {deck.Name}";
            
            Cards.Clear();
            foreach (var card in cards)
            {
                Cards.Add(card);
            }
        }

        // Command für den "Zurück"-Button
        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
    }
}