using Avalonia.Controls;

namespace CapyCard.Views
{
    public partial class ExportDialog : UserControl
    {
        public ExportDialog()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is ViewModels.ExportViewModel vm)
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
