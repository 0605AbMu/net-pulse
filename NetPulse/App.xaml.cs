using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using NetPulse.Core;

namespace NetPulse;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Sentry SDK ni har qanday amaldan oldin ishga tushiramiz
        SentryService.Initialize();

        // 0. Check if invoked by Inno Setup to report installation/upgrade metric: --track-install [--is-upgrade]
        if (e.Args.Contains("--track-install"))
        {
            bool isUpgrade = e.Args.Contains("--is-upgrade");
            var featureName = isUpgrade ? "setup_update" : "setup_install";
            SentryService.TrackFeatureUsage(featureName, new()
            {
                ["is_upgrade"] = isUpgrade.ToString(),
                ["source"] = "inno_setup"
            });
            AppLogger.Log($"[NetPulse] Inno Setup metric reported: {featureName}");
            SentryService.Shutdown();
            Environment.Exit(0);
            return;
        }

        // 1. Check if NetPulse was invoked to execute an internal repair action: --execute-action <ActionId> [adapterName]
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--execute-action" && i + 1 < e.Args.Length)
            {
                if (Enum.TryParse<Models.RepairActionId>(e.Args[i + 1], out var actionId))
                {
                    string? adapter = (i + 2 < e.Args.Length) ? e.Args[i + 2] : null;
                    var netInfo = !string.IsNullOrEmpty(adapter) ? new Models.NetworkInfo { AdapterName = adapter } : null;
                    var result = Task.Run(async () => await RepairEngine.ExecuteActionInternalAsync(actionId, netInfo)).GetAwaiter().GetResult();
                    SentryService.Shutdown();
                    Environment.Exit(result.Success ? 0 : 1);
                    return;
                }
            }
        }

#if !DEBUG
        // 2. Normal startup: if not admin and not explicitly told not to elevate, restart NetPulse as Administrator
        // The Windows UAC prompt will display "NetPulse" (the app itself!)
        if (!AdminHelper.IsAdministrator() && !e.Args.Contains("--no-elevate"))
        {
            AppLogger.Log("[NetPulse] Elevating to Administrator via UAC...");
            var elevated = AdminHelper.RestartAsAdministrator();
            if (elevated)
            {
                SentryService.Shutdown();
                Shutdown();
                return;
            }
        }
#endif

        SetupExceptionHandling();

        base.OnStartup(e);

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            SentryService.Shutdown();
        }
        catch
        {
            // Ignore on exit
        }

        base.OnExit(e);
    }

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            AppLogger.LogError("[AppDomain UNHANDLED EXCEPTION]", ex);
            Trace.TraceError($"[AppDomain UNHANDLED EXCEPTION]: {ex}");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            AppLogger.LogError("[Dispatcher UNHANDLED EXCEPTION]", args.Exception);
            Trace.TraceError($"[Dispatcher UNHANDLED EXCEPTION]: {args.Exception}");
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            AppLogger.LogError("[TaskScheduler UNOBSERVED EXCEPTION]", args.Exception);
            Trace.TraceError($"[TaskScheduler UNOBSERVED EXCEPTION]: {args.Exception}");
        };
    }
}