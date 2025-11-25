using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FlashcardMobile.ViewModels;

namespace FlashcardMobile.Views
{
    public partial class LearnView : UserControl
    {
        private TopLevel? _topLevel;

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<LearnView, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public LearnView()
        {
            InitializeComponent();
            this.SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < 800;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null)
            {
                _topLevel.KeyDown += TopLevelOnKeyDown;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            if (_topLevel != null)
            {
                _topLevel.KeyDown -= TopLevelOnKeyDown;
                _topLevel = null;
            }
        }

        private void TopLevelOnKeyDown(object? sender, KeyEventArgs e)
        {
            if (!IsEffectivelyVisible)
            {
                return;
            }

            if (DataContext is not LearnViewModel vm)
            {
                return;
            }

            if (e.Key is Key.Enter or Key.Return or Key.Space)
            {
                if (vm.AdvanceCommand.CanExecute(null))
                {
                    vm.AdvanceCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}