using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CapyCard.Views
{
    public partial class ImportHelpDialog : UserControl
    {
        public ImportHelpDialog()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is ViewModels.ImportHelpViewModel vm)
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
