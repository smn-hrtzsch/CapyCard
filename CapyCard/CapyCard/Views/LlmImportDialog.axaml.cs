using Avalonia.Controls;

namespace CapyCard.Views;

public partial class LlmImportDialog : UserControl
{
    public LlmImportDialog()
    {
        InitializeComponent();
        this.DataContextChanged += (s, e) =>
        {
            if (DataContext is ViewModels.ImportViewModel vm)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (vm.IsLlmImportVisible)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Focus());
                    }
                };
            }
        };
    }
}
