using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FlashcardApp.ViewModels;
using System;
using System.Threading.Tasks;

namespace FlashcardApp.Views
{
    public partial class CardListView : UserControl
    {
        public CardListView()
        {
            InitializeComponent();
            
            this.DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// Wird aufgerufen, wenn das ViewModel (DataContext) gesetzt wird.
        /// Wir "abonnieren" hier das Dialog-Event des ViewModels.
        /// </summary>
        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is CardListViewModel vm)
            {
                vm.ShowSaveFileDialog += HandleShowSaveDialogAsync;
            }
        }

        /// <summary>
        /// Diese Methode wird vom ViewModel aufgerufen. Sie öffnet den nativen "Speichern unter"-Dialog.
        /// </summary>
        private async Task<string?> HandleShowSaveDialogAsync(string suggestedName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return null;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "PDF speichern unter...",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[] { FilePickerFileTypes.Pdf } 
            });

            // --- HIER IST DIE KORREKTUR ---
            // Die Methode 'TryGetUri' existiert nicht auf IStorageFile.
            // Die korrekte Methode ist 'TryGetLocalPath()', die direkt einen string? zurückgibt.
            return file?.TryGetLocalPath();
        }

        private void CardsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                listBox.SelectedIndex = -1;
                listBox.SelectedItem = null;
            }
        }
    }
}