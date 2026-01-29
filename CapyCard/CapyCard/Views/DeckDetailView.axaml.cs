using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

            // Handle KeyDown at Tunneling stage to catch Escape before anyone else
            this.AddHandler(KeyDownEvent, (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    var focused = topLevel?.FocusManager?.GetFocusedElement();
                    
                    if (focused is TextBox)
                    {
                        this.Focus();
                        e.Handled = true;
                    }
                }
            }, RoutingStrategies.Tunnel);

            // Handle KeyDown at Bubble stage
            this.AddHandler(KeyDownEvent, (sender, e) =>
            {
                if (e.Handled) return;

                if (e.Key == Key.Escape)
                {
                    if (DataContext is DeckDetailViewModel vm)
                    {
                        // HandleEscapeCommand already handles dialogs/dropdowns
                        bool wasAnythingOpen = vm.IsSubDeckSelectionVisible || vm.IsConfirmingDeleteSubDeck || vm.IsSubDeckListOpen;
                        
                        vm.HandleEscapeCommand.Execute(null);
                        
                        // If everything was already closed OR became closed, and user pressed Esc again, go back
                        if (!wasAnythingOpen)
                        {
                            vm.GoBackCommand.Execute(null);
                        }
                        e.Handled = true;
                    }
                }
            }, RoutingStrategies.Bubble);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is DeckDetailViewModel vm)
            {
                vm.OnRequestFileSave += SaveFilePickerAsync;

                // Focus management
                vm.PropertyChanged += (s, args) =>
                {
                    Dispatcher.UIThread.Post(() => HandleFocus(vm));
                };

                vm.ExportViewModel.PropertyChanged += (sender, args) =>
                {
                    Dispatcher.UIThread.Post(() => HandleFocus(vm));
                };
            }
        }

        private void HandleFocus(DeckDetailViewModel vm)
        {
            if (vm.ExportViewModel.IsVisible)
                ExportDialog.Focus();
            else if (vm.IsConfirmingDeleteSubDeck)
                DeleteConfirmationOverlay.Focus();
            else
                this.Focus();
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
