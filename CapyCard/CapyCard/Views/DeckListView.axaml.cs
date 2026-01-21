using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CapyCard.Services;
using CapyCard.ViewModels;

namespace CapyCard.Views
{
    public partial class DeckListView : UserControl
    {
        public DeckListView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            NewDeckTextBox.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            NewDeckTextBox.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        }

        private void OnInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is DeckListViewModel vm && vm.AddDeckCommand.CanExecute(null))
                {
                    vm.AddDeckCommand.Execute(null);
                }
                
                e.Handled = true;
                KeyboardService.ShowKeyboard();
            }
            else if (e.Key == Key.Escape)
            {
                this.Focus();
                e.Handled = true;
            }
        }
    }
}