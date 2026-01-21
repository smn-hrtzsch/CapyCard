using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Window;
using AndroidX.Activity;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;
using CapyCard.Services;
using CapyCard.ViewModels;

namespace CapyCard.Android;

[Activity(
    Label = "CapyCard",
    Name = "com.CapyCode.CapyCard.MainActivity",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustNothing)]
public class MainActivity : AvaloniaMainActivity<App>
{
    public const int PickImageId = 1000;
    private const string BackLogTag = "CapyCard.Back";

    private OnBackPressedCallback? _hardwareBackCallback;
    private ViewTreeObserver.IOnGlobalLayoutListener? _gestureExclusionCleaner;
    private long _lastBackCallbackRefreshMs;
    private string? _lastLoggedVmType;
    private long _lastGestureClearLogMs;

    private Handler? _maintenanceHandler;
    private MaintenanceRunnable? _maintenanceRunnable;

    private IOnBackInvokedCallback? _onBackInvokedCallback;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        Xamarin.Essentials.Platform.Init(this, savedInstanceState);
        PhotoPickerService.Current = new CapyCard.Android.Services.PhotoPickerServiceAndroid();
        CapyCard.Services.ClipboardService.Current = new CapyCard.Android.Services.ClipboardServiceAndroid();
        
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

        // Some controls/containers can request system-gesture exclusion rects (API 29+), which may
        // block the back gesture (edge-swipe) on gesture navigation devices (e.g. Pixel 7).
        // We defensively clear them on layout changes.
        TryInstallGestureExclusionCleaner();

        TryRegisterOnBackInvokedCallback();

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

        Log.Info(BackLogTag, "MainActivity created");
    }

    protected override void OnResume()
    {
        base.OnResume();
        Log.Info(BackLogTag, "MainActivity resumed");
        ClearGestureExclusionRects();
        EnsureBackCallbackTopPriority(force: true);
        LogCurrentViewModelIfChanged();

        StartMaintenanceLoop();
    }

    protected override void OnPause()
    {
        Log.Info(BackLogTag, "MainActivity paused");
        StopMaintenanceLoop();
        base.OnPause();
    }

    protected override void OnStart()
    {
        base.OnStart();
        Log.Info(BackLogTag, "MainActivity started");

        // Use AndroidX OnBackPressedDispatcher directly.
        // This is more reliable across views than relying on Avalonia TopLevel.BackRequested.
        EnsureBackCallbackTopPriority(force: true);
        LogCurrentViewModelIfChanged();
    }

    protected override void OnStop()
    {
        Log.Info(BackLogTag, "MainActivity stopped");

        TryUnregisterOnBackInvokedCallback();

        // Remove callback to avoid leaks / duplicate callbacks after activity recreation.
        if (_hardwareBackCallback != null)
        {
            _hardwareBackCallback.Remove();
            _hardwareBackCallback = null;
            Log.Info(BackLogTag, "OnBackPressedDispatcher callback removed");
        }

        if (_gestureExclusionCleaner != null && Window?.DecorView != null)
        {
            try
            {
                Window.DecorView.ViewTreeObserver?.RemoveOnGlobalLayoutListener(_gestureExclusionCleaner);
            }
            catch
            {
                // ignore
            }
            _gestureExclusionCleaner = null;
            Log.Info(BackLogTag, "Gesture exclusion cleaner removed");
        }

        base.OnStop();
    }

    private void TryRegisterOnBackInvokedCallback()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        if (_onBackInvokedCallback != null)
        {
            return;
        }

        try
        {
            _onBackInvokedCallback = new BackInvokedCallback(this);
            OnBackInvokedDispatcher?.RegisterOnBackInvokedCallback(
                0,
                _onBackInvokedCallback);
            Log.Info(BackLogTag, "OnBackInvokedCallback registered");
        }
        catch (Exception ex)
        {
            Log.Warn(BackLogTag, $"Failed to register OnBackInvokedCallback: {ex.Message}");
            _onBackInvokedCallback = null;
        }
    }

    private void TryUnregisterOnBackInvokedCallback()
    {
        if (_onBackInvokedCallback == null)
        {
            return;
        }

        try
        {
            OnBackInvokedDispatcher?.UnregisterOnBackInvokedCallback(_onBackInvokedCallback);
            Log.Info(BackLogTag, "OnBackInvokedCallback unregistered");
        }
        catch
        {
            // ignore
        }
        finally
        {
            _onBackInvokedCallback = null;
        }
    }

    private sealed class BackInvokedCallback : Java.Lang.Object, IOnBackInvokedCallback
    {
        private readonly MainActivity _activity;

        public BackInvokedCallback(MainActivity activity)
        {
            _activity = activity;
        }

        public void OnBackInvoked()
        {
            try
            {
                Log.Info(BackLogTag, "OnBackInvoked fired");

                var handled = _activity.TryHandleBack(out var currentVmType);
                Log.Info(BackLogTag, $"OnBackInvoked TryHandleBack: CurrentViewModel={currentVmType}; handled={handled}");

                if (!handled)
                {
                    // Default behavior at root: leave app.
                    _activity.Finish();
                }
            }
            catch (Exception ex)
            {
                Log.Error(BackLogTag, $"Exception in OnBackInvoked: {ex}");
                _activity.Finish();
            }
        }
    }

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        try
        {
            if (e != null && e.KeyCode == Keycode.Back && e.Action == KeyEventActions.Up)
            {
                Log.Info(BackLogTag, "DispatchKeyEvent: KEYCODE_BACK (Up)");

                var handled = TryHandleBack(out var currentVmType);
                Log.Info(BackLogTag, $"DispatchKeyEvent TryHandleBack: CurrentViewModel={currentVmType}; handled={handled}");
                if (handled)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(BackLogTag, $"Exception in DispatchKeyEvent: {ex}");
        }

        return base.DispatchKeyEvent(e);
    }

    public override void OnBackPressed()
    {
        // Called for both gesture back and 3-button back.
        try
        {
            Log.Info(BackLogTag, "OnBackPressed invoked");

            var handled = TryHandleBack(out var currentVmType);
            Log.Info(BackLogTag, $"OnBackPressed TryHandleBack: CurrentViewModel={currentVmType}; handled={handled}");
            if (handled)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(BackLogTag, $"Exception in OnBackPressed: {ex}");
        }

        base.OnBackPressed();
    }

    private void TryInstallGestureExclusionCleaner()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            Log.Info(BackLogTag, "Gesture exclusion cleaner skipped (Android < 29)");
            return;
        }

        var decor = Window?.DecorView;
        if (decor == null)
        {
            Log.Warn(BackLogTag, "Gesture exclusion cleaner skipped (DecorView null)");
            return;
        }

        if (_gestureExclusionCleaner != null)
        {
            return;
        }

        _gestureExclusionCleaner = new GestureExclusionCleaner(this);
        decor.ViewTreeObserver?.AddOnGlobalLayoutListener(_gestureExclusionCleaner);
        Log.Info(BackLogTag, "Gesture exclusion cleaner attached");

        ClearGestureExclusionRects();
        EnsureBackCallbackTopPriority(force: false);
        LogCurrentViewModelIfChanged();
    }

    private void ClearGestureExclusionRects()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return;
        }

        var decor = Window?.DecorView;
        if (decor == null)
        {
            return;
        }

        try
        {
            // Clear on decor view.
            ViewCompat.SetSystemGestureExclusionRects(decor, new List<global::Android.Graphics.Rect>());

            // Also clear on the content root if present.
            var content = decor.FindViewById(global::Android.Resource.Id.Content);
            if (content != null)
            {
                ViewCompat.SetSystemGestureExclusionRects(content, new List<global::Android.Graphics.Rect>());
            }

            var now = SystemClock.UptimeMillis();
            if (now - _lastGestureClearLogMs > 3000)
            {
                _lastGestureClearLogMs = now;
                Log.Info(BackLogTag, "Cleared system gesture exclusion rects");
            }
            EnsureBackCallbackTopPriority(force: false);
            LogCurrentViewModelIfChanged();
        }
        catch (Exception ex)
        {
            Log.Warn(BackLogTag, $"Failed to clear gesture exclusion rects: {ex.Message}");
        }
    }

    private void EnsureBackCallbackTopPriority(bool force)
    {
        // Some layers might register their own dispatcher callbacks after ours.
        // Re-adding our callback keeps it at the top (highest priority).
        var now = SystemClock.UptimeMillis();
        if (!force && now - _lastBackCallbackRefreshMs < 1000)
        {
            return;
        }
        _lastBackCallbackRefreshMs = now;

        var createdNew = false;

        if (_hardwareBackCallback == null)
        {
            _hardwareBackCallback = new HardwareBackCallback(this);
            createdNew = true;
        }
        else
        {
            try
            {
                _hardwareBackCallback.Remove();
            }
            catch
            {
                // ignore
            }
        }

        OnBackPressedDispatcher.AddCallback(this, _hardwareBackCallback);
        if (force || createdNew)
        {
            Log.Info(BackLogTag, "OnBackPressedDispatcher callback ensured (top priority)");
        }
    }

    private void StartMaintenanceLoop()
    {
        if (_maintenanceHandler != null)
        {
            return;
        }

        _maintenanceHandler = new Handler(Looper.MainLooper!);
        _maintenanceRunnable = new MaintenanceRunnable(this);
        _maintenanceHandler.PostDelayed(_maintenanceRunnable, 500);
    }

    private void StopMaintenanceLoop()
    {
        if (_maintenanceHandler == null)
        {
            return;
        }

        try
        {
            if (_maintenanceRunnable != null)
            {
                _maintenanceHandler.RemoveCallbacks(_maintenanceRunnable);
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _maintenanceRunnable = null;
            _maintenanceHandler = null;
        }
    }

    private sealed class MaintenanceRunnable : Java.Lang.Object, Java.Lang.IRunnable
    {
        private readonly MainActivity _activity;

        public MaintenanceRunnable(MainActivity activity)
        {
            _activity = activity;
        }

        public void Run()
        {
            try
            {
                if (_activity.IsFinishing || _activity.IsDestroyed)
                {
                    return;
                }

                _activity.ClearGestureExclusionRects();
                _activity.EnsureBackCallbackTopPriority(force: false);
                _activity.LogCurrentViewModelIfChanged();
            }
            catch
            {
                // ignore
            }
            finally
            {
                _activity._maintenanceHandler?.PostDelayed(this, 500);
            }
        }
    }

    private void LogCurrentViewModelIfChanged()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not ISingleViewApplicationLifetime lifetime)
                return;
            if (lifetime.MainView?.DataContext is not MainViewModel vm)
                return;

            var currentVmType = vm.CurrentViewModel?.GetType().FullName ?? "<null>";
            if (!string.Equals(_lastLoggedVmType, currentVmType, StringComparison.Ordinal))
            {
                _lastLoggedVmType = currentVmType;
                Log.Info(BackLogTag, $"Observed CurrentViewModel={currentVmType}");
            }
        }
        catch
        {
            // ignore
        }
    }

    private sealed class GestureExclusionCleaner : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
    {
        private readonly MainActivity _activity;

        public GestureExclusionCleaner(MainActivity activity)
        {
            _activity = activity;
        }

        public void OnGlobalLayout()
        {
            _activity.ClearGestureExclusionRects();
        }
    }

    private bool TryHandleBack(out string currentVmType)
    {
        currentVmType = "<unknown>";

        if (Avalonia.Application.Current?.ApplicationLifetime is not ISingleViewApplicationLifetime lifetime)
        {
            Log.Warn(BackLogTag, "No ISingleViewApplicationLifetime (lifetime is null or different type)");
            return false;
        }

        if (lifetime.MainView is null)
        {
            Log.Warn(BackLogTag, "Lifetime.MainView is null");
            return false;
        }

        if (lifetime.MainView.DataContext is not MainViewModel vm)
        {
            Log.Warn(BackLogTag,
                $"MainView.DataContext is not MainViewModel: {lifetime.MainView.DataContext?.GetType().FullName ?? "<null>"}");
            return false;
        }

        currentVmType = vm.CurrentViewModel?.GetType().FullName ?? "<null>";
        return vm.HandleHardwareBack();
    }

    private void FallbackToSystemBack()
    {
        // Prevent recursion: temporarily disable our callback and let the next handler (or system) run.
        if (_hardwareBackCallback != null)
        {
            _hardwareBackCallback.Enabled = false;
            try
            {
                OnBackPressedDispatcher.OnBackPressed();
            }
            finally
            {
                _hardwareBackCallback.Enabled = true;
            }
        }
        else
        {
            // Last resort.
            Finish();
        }
    }

    private sealed class HardwareBackCallback : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public HardwareBackCallback(MainActivity activity)
            : base(true)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            try
            {
                Log.Info(BackLogTag, "Hardware back pressed");

                var handled = _activity.TryHandleBack(out var currentVmType);
                Log.Info(BackLogTag, $"TryHandleBack: CurrentViewModel={currentVmType}; handled={handled}");

                if (!handled)
                {
                    _activity.FallbackToSystemBack();
                }
            }
            catch (Exception ex)
            {
                Log.Error(BackLogTag, $"Exception in hardware back callback: {ex}");
                _activity.FallbackToSystemBack();
            }
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        CapyCard.Android.Services.PhotoPickerServiceAndroid.OnActivityResult(requestCode, resultCode, data);
    }
}
