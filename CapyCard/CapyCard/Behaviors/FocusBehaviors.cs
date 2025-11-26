using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace CapyCard.Behaviors
{
    /// <summary>
    /// Provides an attached property that focuses the control when the bound value switches to true.
    /// </summary>
    public static class FocusBehaviors
    {
        public static readonly AttachedProperty<bool> FocusOnTrueProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "FocusOnTrue",
                typeof(FocusBehaviors));

        static FocusBehaviors()
        {
            FocusOnTrueProperty.Changed.AddClassHandler<Control, bool>(HandleFocusChange);
        }

        public static bool GetFocusOnTrue(Control control) => control.GetValue(FocusOnTrueProperty);

        public static void SetFocusOnTrue(Control control, bool value) => control.SetValue(FocusOnTrueProperty, value);

        private static void HandleFocusChange(Control control, AvaloniaPropertyChangedEventArgs<bool> change)
        {
            if (!change.NewValue.GetValueOrDefault())
            {
                return;
            }

            if (control is not IInputElement inputElement)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!control.IsEffectivelyVisible)
                {
                    return;
                }

                inputElement.Focus();

                if (control is TextBox textBox)
                {
                    var caret = textBox.Text?.Length ?? 0;
                    textBox.SelectionStart = caret;
                    textBox.SelectionEnd = caret;
                    textBox.CaretIndex = caret;
                }
            });
        }
    }
}
