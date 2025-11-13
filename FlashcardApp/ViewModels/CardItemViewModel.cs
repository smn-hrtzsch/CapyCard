using CommunityToolkit.Mvvm.ComponentModel;
using FlashcardApp.Models;

namespace FlashcardApp.ViewModels
{
    /// <summary>
    /// Ein Wrapper um das Card-Modell, der UI-Zustände wie IsSelected verwaltet.
    /// </summary>
    public partial class CardItemViewModel : ObservableObject
    {
        /// <summary>
        /// Das rohe Daten-Modell
        /// </summary>
        public Card Card { get; }

        // Diese Eigenschaft wird an die CheckBox gebunden
        [ObservableProperty]
        private bool _isSelected;

        public CardItemViewModel(Card card)
        {
            Card = card;
            _isSelected = false; // Standardmäßig nicht ausgewählt
        }
    }
}