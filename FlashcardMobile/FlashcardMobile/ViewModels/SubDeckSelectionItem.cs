using CommunityToolkit.Mvvm.ComponentModel;
using FlashcardMobile.Models;

namespace FlashcardMobile.ViewModels
{
    public partial class SubDeckSelectionItem : ObservableObject
    {
        public Deck Deck { get; }
        public string Name => Deck.Name;

        [ObservableProperty]
        private bool _isSelected;

        public SubDeckSelectionItem(Deck deck)
        {
            Deck = deck;
        }
    }
}
