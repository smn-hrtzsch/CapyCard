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

            // Handle KeyDown at Bubble stage for logic
            this.AddHandler(KeyDownEvent, (sender, e) =>
            {
                if (e.Handled) return;

                if (e.Key == Key.Escape)
                {
                    if (DataContext is DeckListViewModel vm)
                    {
                        if (vm.IsConfirmingDelete)
                        {
                            vm.CancelDeleteCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                }
            }, RoutingStrategies.Bubble);

            // Focus management
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is DeckListViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        Dispatcher.UIThread.Post(() => HandleFocus(vm));
                    };

                    vm.FormatInfoViewModel.PropertyChanged += (sender, args) =>
                    {
                         Dispatcher.UIThread.Post(() => HandleFocus(vm));
                    };
                    vm.ImportHelpViewModel.PropertyChanged += (sender, args) =>
                    {
                         Dispatcher.UIThread.Post(() => HandleFocus(vm));
                    };
                    vm.ImportViewModel.PropertyChanged += (sender, args) =>
                    {
                         Dispatcher.UIThread.Post(() => HandleFocus(vm));
                    };
                }
            };
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
                this.Focus();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < AppConstants.DefaultThreshold;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            NewDeckTextBox.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            
            // Wire up file picker for import
            if (DataContext is DeckListViewModel vm)
            {
                vm.OnRequestFileOpen += OpenFilePickerAsync;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            NewDeckTextBox.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            
            // Unwire file picker
            if (DataContext is DeckListViewModel vm)
            {
                vm.OnRequestFileOpen -= OpenFilePickerAsync;
            }
        }

        private async Task<IStorageFile?> OpenFilePickerAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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