using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownTunnel, RoutingStrategies.Tunnel);
                topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownBubble, RoutingStrategies.Bubble);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownTunnel);
                topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownBubble);
            }
            
            if (DataContext is DeckDetailViewModel vm)
            {
                vm.OnRequestFileSave -= SaveFilePickerAsync;
            }
        }

        private void TopLevelOnKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            if (!IsEffectivelyVisible) return;

            if (e.Key == Key.Escape)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var focused = topLevel?.FocusManager?.GetFocusedElement();
                
                bool isInsideTextBox = focused is TextBox;
                if (!isInsideTextBox && focused is Visual v)
                {
                    isInsideTextBox = v.FindAncestorOfType<TextBox>() != null;
                }

                if (isInsideTextBox)
                {
                    topLevel?.FocusManager?.ClearFocus();
                    this.Focus();
                    e.Handled = true;
                }
            }
        }

        private void TopLevelOnKeyDownBubble(object? sender, KeyEventArgs e)
        {
            if (e.Handled || !IsEffectivelyVisible || DataContext is not DeckDetailViewModel vm) return;

            if (e.Key == Key.Escape)
            {
                bool wasAnythingOpen = vm.IsSubDeckSelectionVisible || vm.IsConfirmingDeleteSubDeck || vm.IsSubDeckListOpen || vm.ExportViewModel.IsVisible;
                
                vm.HandleEscapeCommand.Execute(null);
                
                if (!wasAnythingOpen)
                {
                    if (vm.GoBackCommand.CanExecute(null))
                    {
                        vm.GoBackCommand.Execute(null);
                    }
                }
                e.Handled = true;
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is DeckDetailViewModel vm)
            {
                vm.OnRequestFileSave += SaveFilePickerAsync;

                // Focus management
                vm.PropertyChanged += (s, args) =>
                {
                    // Filter: Only trigger focus logic when relevant visibility flags change
                    // NOT on every property change (like text input)
                    if (args.PropertyName == nameof(DeckDetailViewModel.IsConfirmingDeleteSubDeck) ||
                        args.PropertyName == nameof(DeckDetailViewModel.IsSubDeckSelectionVisible) ||
                        args.PropertyName == nameof(DeckDetailViewModel.IsSubDeckListOpen))
                    {
                        Dispatcher.UIThread.Post(() => HandleFocus(vm));
                    }
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
            {
                // Only clear focus and take focus if we are returning to the main view
                var topLevel = TopLevel.GetTopLevel(this);
                topLevel?.FocusManager?.ClearFocus();
                this.Focus();
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
