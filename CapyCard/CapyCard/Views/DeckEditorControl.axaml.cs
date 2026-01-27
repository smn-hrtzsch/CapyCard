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

        private DeckDetailViewModel? _viewModel;

        public DeckEditorControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            
            if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
            {
                // Adjust layout for mobile (Vertical stacking)
                EditorsGrid.ColumnDefinitions.Clear();
                EditorsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                EditorsGrid.RowDefinitions.Clear();
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
