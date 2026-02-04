using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CapyCard.ViewModels;
using System;

namespace CapyCard.Views
{
    public partial class DeckEditorControl : UserControl
    {
        public static readonly StyledProperty<double> BottomSpacerHeightProperty =
            AvaloniaProperty.Register<DeckEditorControl, double>(nameof(BottomSpacerHeight), defaultValue: 0);

        public double BottomSpacerHeight
        {
            get => GetValue(BottomSpacerHeightProperty);
            set => SetValue(BottomSpacerHeightProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<DeckEditorControl, bool>(nameof(IsCompactMode), defaultValue: false);

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        private DeckDetailViewModel? _viewModel;

        public DeckEditorControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnSizeChanged;
            
            IsCompactModeProperty.Changed.AddClassHandler<DeckEditorControl>((x, e) => x.UpdateLayout((bool)e.NewValue!));
            IsCompactModeProperty.Changed.AddClassHandler<DeckEditorControl>((x, e) => x.UpdateCompactModeClass((bool)e.NewValue!));

            if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
            {
                IsCompactMode = true;
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            IsCompactMode = e.NewSize.Width < AppConstants.StackingThreshold;
        }

        private void UpdateLayout(bool isCompact)
        {
            EditorsGrid.ColumnDefinitions.Clear();
            EditorsGrid.RowDefinitions.Clear();

            if (isCompact)
            {
                // Adjust layout for mobile/compact (Vertical stacking)
                EditorsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                EditorsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Label Front
                EditorsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star)); // Editor Front
                EditorsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Arrow
                EditorsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Label Back
                EditorsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star)); // Editor Back

                // Move elements to new rows/columns
                Grid.SetColumn(FrontLabel, 0); Grid.SetRow(FrontLabel, 0);
                Grid.SetColumn(FrontEditor, 0); Grid.SetRow(FrontEditor, 1);

                Grid.SetColumn(ArrowIcon, 0); Grid.SetRow(ArrowIcon, 2);
                ArrowIcon.RenderTransform = new Avalonia.Media.RotateTransform(90);
                ArrowIcon.Margin = new Thickness(0, 10, 0, 10);

                Grid.SetColumn(BackLabel, 0); Grid.SetRow(BackLabel, 3);
                Grid.SetColumn(BackEditor, 0); Grid.SetRow(BackEditor, 4);
            }
            else
            {
                // Desktop layout (Horizontal)
                EditorsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                EditorsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                EditorsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                EditorsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                EditorsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                Grid.SetColumn(FrontLabel, 0); Grid.SetRow(FrontLabel, 0);
                Grid.SetColumn(BackLabel, 2); Grid.SetRow(BackLabel, 0);

                Grid.SetColumn(FrontEditor, 0); Grid.SetRow(FrontEditor, 1);
                Grid.SetColumn(ArrowIcon, 1); Grid.SetRow(ArrowIcon, 1);
                Grid.SetColumn(BackEditor, 2); Grid.SetRow(BackEditor, 1);

                ArrowIcon.RenderTransform = null;
                ArrowIcon.Margin = new Thickness(12, 0);
            }
        }

        private void UpdateCompactModeClass(bool isCompact)
        {
            Classes.Set("compact", isCompact);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.RequestFrontFocus -= HandleRequestFrontFocus;
            }

            if (DataContext is DeckDetailViewModel vm)
            {
                _viewModel = vm;
                _viewModel.RequestFrontFocus += HandleRequestFrontFocus;
            }
            else
            {
                _viewModel = null;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            if (_viewModel != null)
            {
                _viewModel.RequestFrontFocus -= HandleRequestFrontFocus;
            }
        }

        private void HandleRequestFrontFocus()
        {
            Dispatcher.UIThread.Post(() =>
            {
                FrontEditor.FocusEditor();
            });
        }
    }
}
