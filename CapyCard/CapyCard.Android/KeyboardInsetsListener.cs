using Android.Views;
using AndroidX.Core.View;
using CapyCard.Services;
using System;

namespace CapyCard.Android
{
    public class KeyboardInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        private readonly double _density;

        public KeyboardInsetsListener(double density)
        {
            _density = density;
        }

        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            try
            {
                // 1. Get Navigation Bar Height (System Inset Bottom)
                // Since we use SetDecorFitsSystemWindows(true), our view ends above the nav bar.
                var navInsets = insets.GetInsetsIgnoringVisibility(WindowInsetsCompat.Type.NavigationBars());
                var navHeight = navInsets.Bottom;

                // 2. Keyboard (IME) - Returns absolute height including nav bar area
                var imeType = WindowInsetsCompat.Type.Ime();
                var imeVisible = insets.IsVisible(imeType);
                var imeHeight = insets.GetInsets(imeType).Bottom;
                
                double logicalHeight = 0;
                
                if (imeVisible && imeHeight > navHeight) 
                {
                    // Calculate the part of the keyboard that sticks out *above* the nav bar
                    logicalHeight = (imeHeight - navHeight) / _density;
                }
                
                KeyboardService.NotifyKeyboardHeightChanged(logicalHeight);
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("DEBUG-KB", $"Error in KeyboardInsetsListener: {ex.Message}");
            }

            return ViewCompat.OnApplyWindowInsets(v, insets);
        }
    }
}