using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CapyCard.Services;
using CapyCard.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace CapyCard.Views
{
    public partial class DeckListView : UserControl
    {
        private TopLevel? _topLevel;

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<DeckListView, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public DeckListView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            IsCompactModeProperty.Changed.AddClassHandler<DeckListView>((x, e) => x.UpdateCompactModeClass((bool)e.NewValue!));

            // Focus management
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is DeckListViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(DeckListViewModel.IsConfirmingDelete))
                        {
                            Dispatcher.UIThread.Post(() => HandleFocus(vm));
                        }
                    };

                    vm.FormatInfoViewModel.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(FormatInfoViewModel.IsVisible))
                        {
                            Dispatcher.UIThread.Post(() => HandleFocus(vm));
                        }
                    };
                    vm.ImportHelpViewModel.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(ImportHelpViewModel.IsVisible))
                        {
                            Dispatcher.UIThread.Post(() => HandleFocus(vm));
                        }
                    };
                    vm.ImportViewModel.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(ImportViewModel.IsVisible))
                        {
                            Dispatcher.UIThread.Post(() => HandleFocus(vm));
                        }
                    };
                }
            };
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < AppConstants.DefaultThreshold;
        }

        private void UpdateCompactModeClass(bool isCompact)
        {
            Classes.Set("compact", isCompact);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            NewDeckTextBox.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null)
            {
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownTunnel, RoutingStrategies.Tunnel);
                _topLevel.AddHandler(KeyDownEvent, TopLevelOnKeyDownBubble, RoutingStrategies.Bubble);
            }

            // Wire up file picker for import
            if (DataContext is DeckListViewModel vm)
            {
                vm.OnRequestFileOpen += OpenFilePickerAsync;
            }
            
            this.Focus();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            NewDeckTextBox.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            
            if (_topLevel != null)
            {
                _topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownTunnel);
                _topLevel.RemoveHandler(KeyDownEvent, TopLevelOnKeyDownBubble);
                _topLevel = null;
            }

            // Unwire file picker
            if (DataContext is DeckListViewModel vm)
            {
                vm.OnRequestFileOpen -= OpenFilePickerAsync;
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
            if (e.Handled || !this.IsVisible || DataContext is not DeckListViewModel vm) return;

            if (e.Key == Key.Escape)
            {
                if (vm.ImportHelpViewModel.IsVisible)
                {
                    vm.ImportHelpViewModel.HandleEscapeCommand.Execute(null);
                    e.Handled = true;
                }
                else if (vm.FormatInfoViewModel.IsVisible)
                {
                    vm.FormatInfoViewModel.HandleEscapeCommand.Execute(null);
                    e.Handled = true;
                }
                else if (vm.ImportViewModel.IsVisible)
                {
                    vm.ImportViewModel.HandleEscapeCommand.Execute(null);
                    e.Handled = true;
                }
                else if (vm.IsConfirmingDelete)
                {
                    vm.CancelDeleteCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void HandleFocus(DeckListViewModel vm)
        {
            if (vm.ImportHelpViewModel.IsVisible)
                ImportHelpDialog.Focus();
            else if (vm.FormatInfoViewModel.IsVisible)
                FormatInfoDialog.Focus();
            else if (vm.ImportViewModel.IsVisible)
                ImportDialog.Focus();
            else if (vm.IsConfirmingDelete)
                DeleteConfirmationOverlay.Focus();
            else
            {
                _topLevel?.FocusManager?.ClearFocus();
                this.Focus();
            }
        }

        private async Task<IStorageFile?> OpenFilePickerAsync()
        {
            if (_topLevel == null) return null;

            var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Kartenstapel importieren",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Alle unterstützten Formate")
                    {
                        Patterns = new[] { "*.capycard", "*.apkg", "*.csv", "*.json", "*.txt" }
                    },
                    new FilePickerFileType("CapyCard") { Patterns = new[] { "*.capycard" } },
                    new FilePickerFileType("Anki Deck") { Patterns = new[] { "*.apkg" } },
                    new FilePickerFileType("KI / JSON") { Patterns = new[] { "*.json", "*.txt" } },
                    new FilePickerFileType("CSV") { Patterns = new[] { "*.csv", "*.txt" } }
                }
            });

            return files.FirstOrDefault();
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
