using CommunityToolkit.Mvvm.ComponentModel;
using CapyCard.Models;
using System.Text.RegularExpressions;

namespace CapyCard.ViewModels
{
    /// <summary>
    /// Ein Wrapper um das Card-Modell, der UI-Zustände wie IsSelected verwaltet.
    /// </summary>
    public partial class CardItemViewModel : ObservableObject
    {
        private static readonly Regex ImageRegex = new(@"!\[.*?\]\(.*?\)", RegexOptions.Compiled);

        /// <summary>
        /// Das rohe Daten-Modell
        /// </summary>
        public Card Card { get; }

        public string Front => Card.Front;

        public string Back => Card.Back;

        public string FrontPlain => _frontPlain;

        public string BackPlain => _backPlain;

        public bool FrontHasImages => _frontHasImages;

        public bool BackHasImages => _backHasImages;

        private string _frontPlain = string.Empty;
        private string _backPlain = string.Empty;
        private bool _frontHasImages;
        private bool _backHasImages;

        // Diese Eigenschaft wird an die CheckBox gebunden
        [ObservableProperty]
        private bool _isSelected;

        public CardItemViewModel(Card card)
        {
            Card = card;
            _isSelected = false; // Standardmäßig nicht ausgewählt
            UpdateDerivedFields();
        }

        public void NotifyCardChanged()
        {
            UpdateDerivedFields();
            OnPropertyChanged(nameof(Card));
            OnPropertyChanged(nameof(Front));
            OnPropertyChanged(nameof(Back));
            OnPropertyChanged(nameof(FrontPlain));
            OnPropertyChanged(nameof(BackPlain));
            OnPropertyChanged(nameof(FrontHasImages));
            OnPropertyChanged(nameof(BackHasImages));
        }

        private void UpdateDerivedFields()
        {
            _frontPlain = ImageRegex.Replace(Card.Front ?? string.Empty, string.Empty).Trim();
            _backPlain = ImageRegex.Replace(Card.Back ?? string.Empty, string.Empty).Trim();
            _frontHasImages = ImageRegex.IsMatch(Card.Front ?? string.Empty);
            _backHasImages = ImageRegex.IsMatch(Card.Back ?? string.Empty);
        }
    }
}
