using Avalonia;
using Avalonia.Controls;

namespace CapyCard.Views
{
    public partial class SettingsDialog : UserControl
    {
        public SettingsDialog()
        {
            InitializeComponent();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            var w = e.NewSize.Width;
            Classes.Set("very-narrow", w < 340);
            Classes.Set("narrow", w < 420);
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
