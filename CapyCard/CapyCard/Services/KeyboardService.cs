using System;

namespace CapyCard.Services
{
    public static class KeyboardService
    {
        public static event EventHandler<double>? KeyboardHeightChanged;
        public static event EventHandler<Avalonia.Thickness>? SafeAreaChanged;
        public static Action? RequestShowKeyboard;

        public static void NotifyKeyboardHeightChanged(double height)
        {
            KeyboardHeightChanged?.Invoke(null, height);
        }

        public static void NotifySafeAreaChanged(Avalonia.Thickness safeArea)
        {
            SafeAreaChanged?.Invoke(null, safeArea);
        }

        public static void ShowKeyboard()
        {
            RequestShowKeyboard?.Invoke();
        }
    }
}