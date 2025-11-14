using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FlashcardApp.Behaviors
{
    /// <summary>
    /// Adds a few macOS-style text editing shortcuts to Avalonia text boxes.
    /// </summary>
    public static class MacTextEditingBehavior
    {
        public static readonly AttachedProperty<bool> EnableMacShortcutsProperty =
            AvaloniaProperty.RegisterAttached<TextBox, bool>(
                "EnableMacShortcuts",
                typeof(MacTextEditingBehavior));

        static MacTextEditingBehavior()
        {
            EnableMacShortcutsProperty.Changed.AddClassHandler<TextBox, bool>(HandleEnableChanged);
        }

        public static bool GetEnableMacShortcuts(TextBox textBox) => textBox.GetValue(EnableMacShortcutsProperty);

        public static void SetEnableMacShortcuts(TextBox textBox, bool value) => textBox.SetValue(EnableMacShortcutsProperty, value);

        private static void HandleEnableChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs<bool> change)
        {
            if (change.NewValue.GetValueOrDefault())
            {
                textBox.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            }
            else
            {
                textBox.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            }
        }

        private static void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            var modifiers = e.KeyModifiers;
            var meta = (modifiers & KeyModifiers.Meta) != 0;
            var shift = (modifiers & KeyModifiers.Shift) != 0;
            var handled = false;

            if (meta)
            {
                switch (e.Key)
                {
                    case Key.Back:
                        handled = HandleDeleteToLineStart(textBox);
                        break;
                    case Key.Delete:
                        handled = HandleDeleteToLineEnd(textBox);
                        break;
                    case Key.Left:
                        handled = MoveCaretToLineEdge(textBox, shift, true);
                        break;
                    case Key.Right:
                        handled = MoveCaretToLineEdge(textBox, shift, false);
                        break;
                    case Key.Up:
                        handled = MoveCaretToDocumentEdge(textBox, shift, true);
                        break;
                    case Key.Down:
                        handled = MoveCaretToDocumentEdge(textBox, shift, false);
                        break;
                }
            }
            else if (shift && (e.Key == Key.Up || e.Key == Key.Down))
            {
                handled = ExtendSelectionByLine(textBox, e.Key == Key.Up);
            }

            if (handled)
            {
                e.Handled = true;
            }
        }

        private static bool HandleDeleteToLineStart(TextBox textBox)
        {
            var text = textBox.Text ?? string.Empty;
            var caret = textBox.CaretIndex;

            if (caret <= 0)
            {
                return false;
            }

            var lineStart = FindLineStart(text, caret);
            if (lineStart == caret)
            {
                return false;
            }

            textBox.SelectionStart = lineStart;
            textBox.SelectionEnd = caret;
            textBox.SelectedText = string.Empty;
            textBox.CaretIndex = lineStart;
            return true;
        }

        private static bool HandleDeleteToLineEnd(TextBox textBox)
        {
            var text = textBox.Text ?? string.Empty;
            var caret = textBox.CaretIndex;
            if (caret >= text.Length)
            {
                return false;
            }

            var lineEnd = FindLineEnd(text, caret);
            if (lineEnd == caret)
            {
                return false;
            }

            textBox.SelectionStart = caret;
            textBox.SelectionEnd = lineEnd;
            textBox.SelectedText = string.Empty;
            textBox.CaretIndex = caret;
            return true;
        }

        private static bool MoveCaretToLineEdge(TextBox textBox, bool extendSelection, bool toStart)
        {
            var text = textBox.Text ?? string.Empty;
            var caret = textBox.CaretIndex;
            var target = toStart ? FindLineStart(text, caret) : FindLineEnd(text, caret);

            if (target == caret)
            {
                return false;
            }

            UpdateSelection(textBox, target, extendSelection);
            return true;
        }

        private static bool MoveCaretToDocumentEdge(TextBox textBox, bool extendSelection, bool toStart)
        {
            var text = textBox.Text ?? string.Empty;
            var caret = textBox.CaretIndex;
            var target = toStart ? 0 : text.Length;

            if (target == caret)
            {
                return false;
            }

            UpdateSelection(textBox, target, extendSelection);
            return true;
        }

        private static bool ExtendSelectionByLine(TextBox textBox, bool moveUp)
        {
            var text = textBox.Text ?? string.Empty;
            if (text.Length == 0)
            {
                return false;
            }

            var caret = textBox.CaretIndex;
            var selectionStart = textBox.SelectionStart;
            var selectionEnd = textBox.SelectionEnd;
            var hasSelection = selectionStart != selectionEnd;
            var anchor = hasSelection
                ? (caret == selectionStart ? selectionEnd : selectionStart)
                : caret;

            int target;

            if (moveUp)
            {
                if (!hasSelection || caret <= anchor)
                {
                    target = GetLineUpTarget(text, caret);
                }
                else
                {
                    target = FindLineStart(text, caret);
                }
            }
            else
            {
                if (!hasSelection || caret >= anchor)
                {
                    target = GetLineDownTarget(text, caret);
                }
                else
                {
                    target = FindLineEnd(text, caret);
                    if (target < text.Length)
                    {
                        target = Math.Min(text.Length, target + 1);
                    }
                }
            }

            if (target == caret)
            {
                return false;
            }

            UpdateSelection(textBox, target, true);
            return true;
        }

        private static void UpdateSelection(TextBox textBox, int target, bool extendSelection)
        {
            if (!extendSelection)
            {
                textBox.SelectionStart = target;
                textBox.SelectionEnd = target;
                textBox.CaretIndex = target;
                return;
            }

            var caret = textBox.CaretIndex;
            var selectionStart = textBox.SelectionStart;
            var selectionEnd = textBox.SelectionEnd;

            var hasSelection = selectionStart != selectionEnd;
            var anchor = hasSelection
                ? (caret == selectionStart ? selectionEnd : selectionStart)
                : caret;

            textBox.CaretIndex = target;

            var start = Math.Min(anchor, target);
            var end = Math.Max(anchor, target);

            textBox.SelectionStart = start;
            textBox.SelectionEnd = end;
        }

        private static int FindLineStart(string text, int caret)
        {
            if (string.IsNullOrEmpty(text) || caret <= 0)
            {
                return 0;
            }

            var searchStart = Math.Min(caret - 1, text.Length - 1);
            var lineBreak = text.LastIndexOf('\n', searchStart);
            return lineBreak == -1 ? 0 : lineBreak + 1;
        }

        private static int FindLineEnd(string text, int caret)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var lineBreak = text.IndexOf('\n', caret);
            return lineBreak == -1 ? text.Length : lineBreak;
        }

        private static int GetLineDownTarget(string text, int caret)
        {
            if (caret >= text.Length)
            {
                return caret;
            }

            var lineEnd = FindLineEnd(text, caret);
            if (lineEnd == caret && caret < text.Length)
            {
                lineEnd = FindLineEnd(text, Math.Min(text.Length, caret + 1));
            }

            if (lineEnd >= text.Length)
            {
                return text.Length;
            }

            return Math.Min(text.Length, lineEnd + 1);
        }

        private static int GetLineUpTarget(string text, int caret)
        {
            if (caret <= 0)
            {
                return 0;
            }

            var currentLineStart = FindLineStart(text, caret);
            if (currentLineStart == caret && caret > 0)
            {
                currentLineStart = FindLineStart(text, caret - 1);
            }

            if (currentLineStart <= 0)
            {
                return 0;
            }

            return FindLineStart(text, Math.Max(0, currentLineStart - 1));
        }
    }
}
