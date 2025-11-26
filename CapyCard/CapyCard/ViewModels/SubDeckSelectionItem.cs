using CommunityToolkit.Mvvm.ComponentModel;
using CapyCard.Models;

namespace CapyCard.ViewModels
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
