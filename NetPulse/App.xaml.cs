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
        
#if !DEBUG
        // 2. Normal startup: if not admin and not explicitly told not to elevate, restart NetPulse as Administrator
        // The Windows UAC prompt will display "NetPulse" (the app itself!)
        if (!AdminHelper.IsAdministrator() && !e.Args.Contains("--no-elevate"))
        {
            AppLogger.Log("[NetPulse] Elevating to Administrator via UAC...");
            var elevated = AdminHelper.RestartAsAdministrator();
            if (elevated)
            {
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

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            AppLogger.LogError("\n[AppDomain UNHANDLED EXCEPTION]", ex);
            Trace.TraceError($"[AppDomain UNHANDLED EXCEPTION]: {ex}");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            AppLogger.LogError("\n[Dispatcher UNHANDLED EXCEPTION]", args.Exception);
            Trace.TraceError($"[Dispatcher UNHANDLED EXCEPTION]: {args.Exception}");
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            AppLogger.LogError("\n[TaskScheduler UNOBSERVED EXCEPTION]", args.Exception);
            Trace.TraceError($"[TaskScheduler UNOBSERVED EXCEPTION]: {args.Exception}");
        };
    }
}