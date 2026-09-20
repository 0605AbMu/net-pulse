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
        // 1. Check if NetPulse was invoked as elevated script runner: --run-script <scriptPath> <logPath>
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--run-script" && i + 2 < e.Args.Length)
            {
                var scriptPath = e.Args[i + 1];
                var logPath = e.Args[i + 2];
                var exitCode = ExecuteScriptElevated(scriptPath, logPath);
                Shutdown(exitCode);
                return;
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

    private static int ExecuteScriptElevated(string scriptPath, string logPath)
    {
        try
        {
            if (!File.Exists(scriptPath))
            {
                File.WriteAllText(logPath, "Skript fayli topilmadi: " + scriptPath, System.Text.Encoding.UTF8);
                return 1;
            }

            var script = File.ReadAllText(scriptPath, System.Text.Encoding.UTF8);
            var result = AdminHelper.RunScriptInProcess(script);

            var output = !string.IsNullOrEmpty(result.Output) ? result.Output : result.Error;
            File.WriteAllText(logPath, output ?? string.Empty, System.Text.Encoding.UTF8);
            return result.ExitCode;
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(logPath, "Runner Exception: " + ex.Message, System.Text.Encoding.UTF8);
            }
            catch { }
            return 1;
        }
    }
}