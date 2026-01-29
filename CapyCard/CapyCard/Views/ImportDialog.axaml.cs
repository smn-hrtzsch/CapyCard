using Avalonia.Controls;

namespace CapyCard.Views
{
    public partial class ImportDialog : UserControl
    {
        public ImportDialog()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is ViewModels.ImportViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        // Always try to keep focus on this or sub-elements when visible
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
