using Foundation;
using UIKit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;
using FlashcardMobile.Services;
using System.Linq;

namespace FlashcardMobile.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    public AppDelegate()
    {
        // Subscribe to Keyboard Events
        NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillShowNotification, OnKeyboardNotification);
        NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillHideNotification, OnKeyboardNotification);
    }

    private void OnKeyboardNotification(NSNotification notification)
    {
        try
        {
            bool isShowing = notification.Name == UIKeyboard.WillShowNotification;
            double height = 0;

            if (isShowing)
            {
                // Get Keyboard Frame
                var frame = UIKeyboard.FrameEndFromNotification(notification);
                height = frame.Height;

                // Subtract Safe Area Bottom (Home Indicator)
                // Avalonia usually pads the view for the Home Indicator.
                // The keyboard sits on top of the Home Indicator area.
                // If we push up by full Keyboard Height, we push up by (Keyboard + HomeIndicatorPadding).
                // But we already have HomeIndicatorPadding in the view layout.
                // So we should push up by (Keyboard - HomeIndicatorPadding).
                var window = UIApplication.SharedApplication.ConnectedScenes
                    .OfType<UIWindowScene>()
                    .SelectMany(s => s.Windows)
                    .FirstOrDefault(w => w.IsKeyWindow);

                if (window != null)
                {
                    height -= window.SafeAreaInsets.Bottom;
                }
            }

            if (height < 0) height = 0;

            KeyboardService.NotifyKeyboardHeightChanged(height);
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"[iOS Keyboard] Error: {ex}");
        }
    }
}
