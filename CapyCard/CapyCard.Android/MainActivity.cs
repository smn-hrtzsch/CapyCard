using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using CapyCard.Services;

namespace CapyCard.Android;

[Activity(
    Label = "CapyCard",
    Name = "com.CapyCard.CapyCard.MainActivity",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustNothing)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        if (Window != null)
        {
            WindowCompat.SetDecorFitsSystemWindows(Window, true);
        }
        
        var contentView = Window?.DecorView.FindViewById(global::Android.Resource.Id.Content);
        if (contentView != null)
        {
            var density = Resources?.DisplayMetrics?.Density ?? 1.0;
            ViewCompat.SetOnApplyWindowInsetsListener(contentView, new KeyboardInsetsListener(density));
        }

        // --- KeyboardService.RequestShowKeyboard implementation for Android ---
        KeyboardService.RequestShowKeyboard = () =>
        {
            var imm = (InputMethodManager?)GetSystemService(Context.InputMethodService);
            var view = CurrentFocus ?? Window?.DecorView; // Try to get the current focused view, fallback to window decor
            if (view != null && imm != null)
            {
                imm.ShowSoftInput(view, ShowFlags.Implicit);
            }
        };
    }
}
