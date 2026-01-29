using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CapyCard.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CapyCard.Views
{
    public partial class DeckDetailView : UserControl
    {
        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<DeckDetailView, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public DeckDetailView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is DeckDetailViewModel vm)
            {
                vm.OnRequestFileSave += SaveFilePickerAsync;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            
            if (DataContext is DeckDetailViewModel vm)
            {
                vm.OnRequestFileSave -= SaveFilePickerAsync;
            }
        }

        private async Task<IStorageFile?> SaveFilePickerAsync(string suggestedName, string extension)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            // Remove extension from suggested name to avoid double extension
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(suggestedName);

            var fileType = extension switch
            {
                ".capycard" => new FilePickerFileType("CapyCard") { Patterns = new[] { "*.capycard" } },
                ".apkg" => new FilePickerFileType("Anki Deck") { Patterns = new[] { "*.apkg" } },
                ".csv" => new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } },
                _ => new FilePickerFileType("CapyCard") { Patterns = new[] { "*.capycard" } }
            };

            return await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Kartenstapel exportieren",
                SuggestedFileName = nameWithoutExtension,
                DefaultExtension = extension.TrimStart('.'),
                FileTypeChoices = new[] { fileType }
            });
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < AppConstants.HeaderThreshold;
        }
    }
}
