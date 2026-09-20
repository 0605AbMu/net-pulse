using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using NetPulse.Models;

namespace NetPulse.Core;

public static class AdminHelper
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string GetAppExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var baseDirExe = Path.Combine(AppContext.BaseDirectory, "NetPulse.exe");
        if (File.Exists(baseDirExe))
        {
            return baseDirExe;
        }

        return path ?? string.Empty;
    }

    public static bool RestartAsAdministrator(string? additionalArgs = null)
    {
        try
        {
            var exePath = GetAppExecutablePath();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = additionalArgs ?? string.Empty,
                WorkingDirectory = Environment.SystemDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };

            var proc = Process.Start(startInfo);
            return proc != null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes an internal NetPulse repair action with Administrator elevation.
    /// Runs NetPulse itself with Verb = 'runas' and '--execute-action', completely eliminating external script files.
    /// </summary>
    public static async Task<RepairActionResult> RunElevatedActionAsync(RepairActionId actionId, string? adapterName = null)
    {
        if (IsAdministrator())
        {
            var netInfo = !string.IsNullOrEmpty(adapterName) ? new NetworkInfo { AdapterName = adapterName } : null;
            return await RepairEngine.ExecuteActionInternalAsync(actionId, netInfo);
        }

        try
        {
            var appExe = GetAppExecutablePath();
            if (string.IsNullOrEmpty(appExe) || !File.Exists(appExe))
            {
                return RepairActionResult.Fail("Dastur ijrochi fayli topilmadi.");
            }

            var args = $"--execute-action {actionId}";
            if (!string.IsNullOrEmpty(adapterName))
            {
                args += $" \"{adapterName}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = appExe,
                Arguments = args,
                WorkingDirectory = Environment.SystemDirectory,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return RepairActionResult.Fail("Administrator jarayonini ishga tushirib bo'lmadi.");
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0
                ? RepairActionResult.Ok("Amal muvaffaqiyatli bajarildi.")
                : RepairActionResult.Fail("Amalni bajarishda xatolik yuz berdi.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return RepairActionResult.Fail("Foydalanuvchi Administrator huquqini berishni rad etdi (UAC bekor qilindi).");
        }
        catch (Exception ex)
        {
            return RepairActionResult.Fail($"Xatolik yuz berdi: {ex.Message}");
        }
    }

    private static string ResolveSystemExecutablePath(string fileName)
    {
        if (Path.IsPathRooted(fileName) && File.Exists(fileName))
        {
            return fileName;
        }

        var systemPath = Path.Combine(Environment.SystemDirectory, fileName);
        if (File.Exists(systemPath))
        {
            return systemPath;
        }

        return fileName;
    }

    public static async Task<ProcessExecutionResult> RunCommandAsync(string fileName, string arguments, bool requireAdmin = false)
    {
        var resolvedFileName = ResolveSystemExecutablePath(fileName);

        if (requireAdmin && !IsAdministrator())
        {
            try
            {
                var elevatedInfo = new ProcessStartInfo
                {
                    FileName = resolvedFileName,
                    Arguments = arguments,
                    WorkingDirectory = Environment.SystemDirectory,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var elevatedProcess = Process.Start(elevatedInfo);
                if (elevatedProcess == null)
                    return new ProcessExecutionResult(-1, string.Empty, "Jarayonni ishga tushirib bo'lmadi.");

                await elevatedProcess.WaitForExitAsync();
                return new ProcessExecutionResult(elevatedProcess.ExitCode, string.Empty, string.Empty);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new ProcessExecutionResult(-1, string.Empty, "UAC bekor qilindi.");
            }
            catch (Exception ex)
            {
                return new ProcessExecutionResult(-1, string.Empty, ex.Message);
            }
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedFileName,
                Arguments = arguments,
                WorkingDirectory = Environment.SystemDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return new ProcessExecutionResult(-1, string.Empty, "Jarayonni ishga tushirib bo'lmadi.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new ProcessExecutionResult(process.ExitCode, stdout, stderr);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5) // Access is denied
        {
            // Fallback: If UseShellExecute=false failed with Access is denied, attempt elevated execution
            try
            {
                var elevatedFallback = new ProcessStartInfo
                {
                    FileName = resolvedFileName,
                    Arguments = arguments,
                    WorkingDirectory = Environment.SystemDirectory,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var elevatedProc = Process.Start(elevatedFallback);
                if (elevatedProc != null)
                {
                    await elevatedProc.WaitForExitAsync();
                    return new ProcessExecutionResult(elevatedProc.ExitCode, string.Empty, string.Empty);
                }
            }
            catch { }

            return new ProcessExecutionResult(-1, string.Empty, ex.Message);
        }
        catch (Exception ex)
        {
            return new ProcessExecutionResult(-1, string.Empty, ex.Message);
        }
    }

    public static ProcessExecutionResult RunScriptInProcess(string script)
    {
        try
        {
            var psPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = File.Exists(psPath) ? psPath : "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command -",
                WorkingDirectory = Environment.SystemDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ProcessExecutionResult(-1, string.Empty, "PowerShell jarayonini boshlab bo'lmadi.");
            }

            process.StandardInput.Write(script);
            process.StandardInput.Close();

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new ProcessExecutionResult(process.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return new ProcessExecutionResult(-1, string.Empty, ex.Message);
        }
    }
}
