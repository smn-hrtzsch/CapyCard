using Avalonia.Controls;

namespace CapyCard.Views
{
    public partial class FormatInfoDialog : UserControl
    {
        public FormatInfoDialog()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is ViewModels.FormatInfoViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (vm.IsVisible)
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Focus());
                        }
                    };
                }
            };
        }
    }
}
