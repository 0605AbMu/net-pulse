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

        var assemblyLocation = typeof(AdminHelper).Assembly.Location;
        var exePath = Path.ChangeExtension(assemblyLocation, ".exe");
        if (File.Exists(exePath))
        {
            return exePath;
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
    /// Executes a PowerShell script with elevation if needed.
    /// Uses NetPulse executable itself with Verb = 'runas' so the Windows UAC prompt shows 'NetPulse', not 'Windows PowerShell'.
    /// </summary>
    public static async Task<ProcessExecutionResult> RunElevatedScriptAsync(string script)
    {
        if (IsAdministrator())
        {
            // Already admin: execute directly in-process without any UAC prompt
            return await RunScriptInProcessAsync(script);
        }

        // Not admin: create a temporary script file and run it elevated through NetPulse itself
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"netpulse_{Guid.NewGuid():N}.ps1");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"netpulse_{Guid.NewGuid():N}.log");

        try
        {
            var appExe = GetAppExecutablePath();
            ProcessStartInfo startInfo;

            if (!string.IsNullOrEmpty(appExe) && File.Exists(appExe))
            {
                // Write pure script for NetPulse elevated runner
                await File.WriteAllTextAsync(tempScriptPath, script, Encoding.UTF8);

                // Launch NetPulse itself with Verb = "runas"
                // The UAC prompt displays "NetPulse", NOT "Windows PowerShell"!
                startInfo = new ProcessStartInfo
                {
                    FileName = appExe,
                    Arguments = $"--run-script \"{tempScriptPath}\" \"{tempLogPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
            }
            else
            {
                // Fallback to powershell wrapper if app executable path cannot be resolved
                var escapedLogPath = tempLogPath.Replace("'", "''");
                var scriptWrapper = $@"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Continue'
$logFile = '{escapedLogPath}'

try {{
    & {{
{script}
    }} *>&1 | Out-File -FilePath $logFile -Encoding utf8
    if ($LASTEXITCODE -ne $null) {{ exit $LASTEXITCODE }} else {{ exit 0 }}
}} catch {{
    $_.ToString() | Out-File -FilePath $logFile -Append -Encoding utf8
    exit 1
}}
";
                await File.WriteAllTextAsync(tempScriptPath, scriptWrapper, Encoding.UTF8);

                startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempScriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ProcessExecutionResult(-1, string.Empty, "Jarayonni ishga tushirib bo'lmadi.");
            }

            await process.WaitForExitAsync();

            var output = File.Exists(tempLogPath) ? await File.ReadAllTextAsync(tempLogPath) : string.Empty;
            return new ProcessExecutionResult(process.ExitCode, output, string.Empty);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return new ProcessExecutionResult(-1, string.Empty, "Foydalanuvchi Administrator huquqini berishni rad etdi (UAC bekor qilindi).");
        }
        catch (Exception ex)
        {
            return new ProcessExecutionResult(-1, string.Empty, $"Xatolik yuz berdi: {ex.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(tempScriptPath)) File.Delete(tempScriptPath);
                if (File.Exists(tempLogPath)) File.Delete(tempLogPath);
            }
            catch
            {
                // Ignore temp cleanup errors
            }
        }
    }

    public static async Task<ProcessExecutionResult> RunScriptInProcessAsync(string script)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command -",
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

            await process.StandardInput.WriteAsync(script);
            process.StandardInput.Close();

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new ProcessExecutionResult(process.ExitCode, await outTask, await errTask);
        }
        catch (Exception ex)
        {
            return new ProcessExecutionResult(-1, string.Empty, ex.Message);
        }
    }

    public static async Task<ProcessExecutionResult> RunCommandAsync(string fileName, string arguments, bool requireAdmin = false)
    {
        if (requireAdmin && !IsAdministrator())
        {
            var script = $"& '{fileName}' {arguments}";
            return await RunElevatedScriptAsync(script);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
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
        catch (Exception ex)
        {
            return new ProcessExecutionResult(-1, string.Empty, ex.Message);
        }
    }

    public static async Task<ProcessExecutionResult> RunPowerShellCommandAsync(string script, bool requireAdmin = false)
    {
        if (requireAdmin)
        {
            return await RunElevatedScriptAsync(script);
        }

        return await RunScriptInProcessAsync(script);
    }

    public static ProcessExecutionResult RunScriptInProcess(string script)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command -",
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
