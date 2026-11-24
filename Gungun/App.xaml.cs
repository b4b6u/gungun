using Gungun.Services;
using Microsoft.UI.Xaml;

namespace Gungun;

/// <summary>
/// Application entry point. Gungun is a background agent: the overlay window is created,
/// primed (hotkeys + mouse hook attach to its HWND) and immediately hidden.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\Gungun.SingleInstance";

    private static Mutex? _singleInstance;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();

        // Three nets, because WinUI only reports the first one. The UI handler catches dispatch
        // exceptions; the AppDomain handler catches background threads, finalizers and anything
        // thrown during shutdown; TaskScheduler catches faults nobody awaited.
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Runs before the repository, settings store or logger touch the disk.
        MigrateLegacyWorkspace();

        if (!TryClaimSingleInstance())
        {
            // A second copy would double-register Alt+N and the mouse hook.
            Exit();
            return;
        }

        // Tripwire for the offline promise: any HttpClient call to something other than a
        // calendar provider is written to gungun.log instead of happening silently.
        NetworkGuard.Start();

        // Packaged builds get a desktop shortcut on first launch (MSIX has no install scripts);
        // the portable build has no package identity, so this is a no-op there.
        DesktopShortcut.Ensure();

        try
        {
            _window = new MainWindow();

            // The saved look is painted before the first frame, so the overlay never flashes Paper.
            _window.ViewModel.ApplySavedTheme();

            // Refresh the licence in the background: Pro keeps its grace window if Dodo is
            // unreachable, and the settings window repaints itself when the answer arrives.
            _ = Gungun.Services.Entitlement.Entitlements.Current.RefreshAsync();

            // Activate once so the XAML/composition tree is live, then hide immediately:
            // Gungun must not present a primary window on launch.
            _window.Activate();
            _window.HideToBackground();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Gungun failed to start", ex);
            Exit();
        }
    }

    /// <summary>
    /// One-time move of the folder the app used under an older name - %LOCALAPPDATA%\Nectar, and
    /// %LOCALAPPDATA%\TopSpace before that - into the Gungun folder, renaming the database, its
    /// SQLite companions and the log with it. Best effort: a failure leaves the old files
    /// untouched rather than starting from an empty workspace.
    /// </summary>
    private static void MigrateLegacyWorkspace()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var legacy = WorkspaceMigration.FolderToAdopt(
                name => Directory.Exists(Path.Combine(local, name)));

            if (legacy is null)
            {
                return;
            }

            var current = Path.Combine(local, WorkspaceMigration.CurrentFolder);
            Directory.Move(Path.Combine(local, legacy), current);

            var names = new List<string>();
            foreach (var file in new DirectoryInfo(current).GetFiles())
            {
                names.Add(file.Name);
            }

            foreach (var (from, to) in WorkspaceMigration.Renames(names))
            {
                var source = Path.Combine(current, from);
                var target = Path.Combine(current, to);
                if (File.Exists(source) && !File.Exists(target))
                {
                    File.Move(source, target);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Workspace migration from the legacy folder failed", ex);
        }
    }
    private static bool TryClaimSingleInstance()
    {
        try
        {
            _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
            return createdNew;
        }
        catch (Exception ex)
        {
            // If the mutex cannot be created we still start; duplicates are the lesser evil.
            AppLogger.Log("Single-instance check failed", ex);
            return true;
        }
    }

    /// <summary>
    /// Exceptions raised on the XAML dispatcher. Handled is set so the overlay does not vanish
    /// mid-session; the cost is that a genuinely corrupt UI state survives until the next action
    /// fails, which is why every one of these is written to crashlog.txt as well as gungun.log.
    /// </summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        CrashReporter.Write("Unhandled UI exception (dispatcher)", e.Exception);
        AppLogger.Log("Unhandled UI exception", e.Exception);

        e.Handled = true;
    }

    /// <summary>
    /// Fatal exceptions from non-UI threads. Nothing can prevent termination here, so the only job
    /// is to leave a record and give the log a chance to flush.
    /// </summary>
    private static void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var verdict = e.IsTerminating ? "terminating" : "non-terminating";

        CrashReporter.Write($"AppDomain unhandled exception ({verdict})", exception);
        if (exception is not null)
        {
            AppLogger.Log($"AppDomain unhandled exception ({verdict})", exception);
        }
        else
        {
            AppLogger.Log($"AppDomain unhandled exception ({verdict}): {e.ExceptionObject}");
        }
    }

    /// <summary>
    /// Faulted tasks that were never awaited. Without SetObserved these are rethrown on the
    /// finalizer thread and take the process down long after the failure was recoverable.
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashReporter.Write("Unobserved task exception", e.Exception);
        AppLogger.Log("Unobserved task exception", e.Exception);

        e.SetObserved();
    }
}
