using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FlashcardMobile.ViewModels;
using System;

namespace FlashcardMobile.Views
{
    public partial class DeckDetailView : UserControl
    {
        private DeckDetailViewModel? _viewModel;

        public DeckDetailView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Dispatcher.UIThread.Post(() => FrontTextBox.Focus());
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
                FrontTextBox.Focus();
                FrontTextBox.CaretIndex = 0;
            });
        }
    }
}