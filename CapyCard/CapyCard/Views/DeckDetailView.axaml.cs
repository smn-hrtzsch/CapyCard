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
        private TopLevel? _topLevel;

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
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null)
            {
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownTunnel, RoutingStrategies.Tunnel);
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownBubble, RoutingStrategies.Bubble);
            }
            
            this.Focus();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            if (_topLevel != null)
            {
                _topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownTunnel);
                _topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownBubble);
                _topLevel = null;
            }
            
            if (DataContext is DeckDetailViewModel vm)
            {
                vm.OnRequestFileSave -= SaveFilePickerAsync;
            }
        }

        private void TopLevelOnKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            if (!this.IsVisible) return;

            if (e.Key == Key.Escape)
            {
                var focused = _topLevel?.FocusManager?.GetFocusedElement();
                
                bool isInsideTextBox = focused is TextBox;
                if (!isInsideTextBox && focused is Visual v)
                {
                    isInsideTextBox = v.FindAncestorOfType<TextBox>() != null;
                }

                if (isInsideTextBox)
                {
                    _topLevel?.FocusManager?.ClearFocus();
                    this.Focus();
                    e.Handled = true;
                }
            }
        }

        private void TopLevelOnKeyDownBubble(object? sender, KeyEventArgs e)
        {
            if (e.Handled || !this.IsVisible || DataContext is not DeckDetailViewModel vm) return;

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
                    if (args.PropertyName == nameof(DeckDetailViewModel.IsConfirmingDeleteSubDeck) ||
                        args.PropertyName == nameof(DeckDetailViewModel.IsSubDeckSelectionVisible) ||
                        args.PropertyName == nameof(DeckDetailViewModel.IsSubDeckListOpen))
                    {
                        Dispatcher.UIThread.Post(() => HandleFocus(vm));
                    }
                };

                vm.ExportViewModel.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(ExportViewModel.IsVisible))
                    {
                        Dispatcher.UIThread.Post(() => HandleFocus(vm));
                    }
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
                _topLevel?.FocusManager?.ClearFocus();
                this.Focus();
            }
        }

        private async Task<IStorageFile?> SaveFilePickerAsync(string suggestedName, string extension)
        {
            if (_topLevel == null) return null;

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(suggestedName);

            var fileType = extension switch
            {
                ".capycard" => new FilePickerFileType("CapyCard") { Patterns = new[] { "*.capycard" } },
                ".apkg" => new FilePickerFileType("Anki Deck") { Patterns = new[] { "*.apkg" } },
                ".csv" => new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } },
                _ => new FilePickerFileType("CapyCard") { Patterns = new[] { "*.capycard" } }
            };

            return await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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
