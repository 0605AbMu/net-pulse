using System.Diagnostics;

namespace NetPulse.Core;

/// <summary>
/// Provides logging methods that are only active in DEBUG configuration.
/// In RELEASE builds, the C# compiler completely strips out calls to these methods.
/// </summary>
public static class AppLogger
{
    [Conditional("DEBUG")]
    public static void Log(string message)
    {
        Console.WriteLine(message);
    }

    [Conditional("DEBUG")]
    public static void Log(string format, params object?[] args)
    {
        Console.WriteLine(string.Format(format, args));
    }

    [Conditional("DEBUG")]
    public static void LogError(string message, Exception? ex = null)
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
}
