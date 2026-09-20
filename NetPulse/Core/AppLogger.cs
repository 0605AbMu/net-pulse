using System.Diagnostics;
using Sentry;

namespace NetPulse.Core;

/// <summary>
/// Ilova loglarini konsolga chiqaradi, shuningdek barcha log va xatoliklarni
/// Sentry tizimiga (Structured Logs, Breadcrumbs va Issues/Errors) uzatadi.
/// </summary>
public static class AppLogger
{
    public static void Log(string message)
    {
        Debug.WriteLine(message);
        Console.WriteLine(message);

        try
        {
            // Sentry Breadcrumbs va Sentry Structured Logs
            SentrySdk.AddBreadcrumb(message, category: "app", level: BreadcrumbLevel.Info);
            SentrySdk.Logger.LogInfo(message);
        }
        catch
        {
            // Logging failure should never crash the app
        }
    }

    public static void Log(string format, params object?[] args)
    {
        var message = string.Format(format, args);
        Log(message);
    }

    public static void LogError(string message, Exception? ex = null)
    {
        Debug.WriteLine($"[ERROR] {message}: {ex}");

        try
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if (ex != null)
            {
                Console.Error.WriteLine($"{message}: {ex}");
            }
            else
            {
                Console.Error.WriteLine(message);
            }
            Console.ResetColor();
        }
        catch
        {
            // Ignore console color exceptions
        }

        try
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
            SentrySdk.AddBreadcrumb(fullMessage, category: "error", level: BreadcrumbLevel.Error);
            SentrySdk.Logger.LogError(fullMessage);

            if (ex != null)
            {
                SentrySdk.CaptureException(ex, scope =>
                {
                    scope.SetTag("error_context", message);
                    scope.SetTag("device_id", DeviceIdentifier.GetDeviceId());
                });
            }
            else
            {
                SentrySdk.CaptureMessage(message, SentryLevel.Error);
            }
        }
        catch
        {
            // Sentry error dispatching should never crash the app
        }
    }
}
