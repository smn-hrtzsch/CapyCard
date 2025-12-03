using Foundation;
using UIKit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;
using CapyCard.Services;
using System.Linq;
using System.Threading.Tasks;

namespace CapyCard.iOS;

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
        // Register Platform Services
        PhotoPickerService.Current = new CapyCard.iOS.Services.PhotoPickerServiceiOS();
        ClipboardService.Current = new CapyCard.iOS.Services.ClipboardServiceiOS();

        // Subscribe to Keyboard Events
        NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillShowNotification, OnKeyboardNotification);
        NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillHideNotification, OnKeyboardNotification);

        KeyboardService.RequestShowKeyboard = () =>
        {
            Task.Delay(100).ContinueWith(t =>
            {
                UIApplication.SharedApplication.InvokeOnMainThread(() =>
                {
                    var window = UIApplication.SharedApplication.ConnectedScenes
                        .OfType<UIWindowScene>()
                        .SelectMany(s => s.Windows)
                        .FirstOrDefault(w => w.IsKeyWindow);

                    if (window != null)
                    {
                        var firstResponder = FindFirstResponder(window);
                        if (firstResponder != null)
                        {
                            firstResponder.ResignFirstResponder();
                            firstResponder.BecomeFirstResponder();
                        }
                    }
                });
            });
        };
    }

    private UIView? FindFirstResponder(UIView view)
    {
        if (view.IsFirstResponder) return view;
        foreach (var sub in view.Subviews)
        {
            var found = FindFirstResponder(sub);
            if (found != null) return found;
        }
        return null;
    }

    private void OnKeyboardNotification(NSNotification notification)
    {
        try
        {
            bool isShowing = notification.Name == UIKeyboard.WillShowNotification;
            double height = 0;

            if (isShowing)
            {
                var frame = UIKeyboard.FrameEndFromNotification(notification);
                height = frame.Height;

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

