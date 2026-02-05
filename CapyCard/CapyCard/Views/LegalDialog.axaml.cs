using Avalonia;
using Avalonia.Controls;

namespace CapyCard.Views
{
    public partial class LegalDialog : UserControl
    {
        public LegalDialog()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsVisibleProperty && change.GetNewValue<bool>())
            {
                this.Focus();
            }
        }
    }
}
