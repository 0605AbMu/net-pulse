using System.IO;
using System.Management;
using System.Text;
using Microsoft.Win32;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Core;

public static class RepairEngine
{
    private static readonly RepairAction[] AvailableActions =
    [
        new RepairAction
        {
            Id = RepairActionId.OptimizeAll,
            Category = RepairCategory.Recommended,
            IsRecommended = true,
            RequiresAdmin = true
        },
        new RepairAction
        {
            Id = RepairActionId.TcpAutoTuning,
            Category = RepairCategory.Recommended,
            RequiresAdmin = true
        },
        new RepairAction
        {
            Id = RepairActionId.NetworkThrottling,
            Category = RepairCategory.Recommended,
            RequiresAdmin = true
        },
        new RepairAction
        {
            Id = RepairActionId.PowerSaving,
            Category = RepairCategory.Recommended,
            RequiresAdmin = false
        },
        new RepairAction
        {
            Id = RepairActionId.DnsCloudflare,
            Category = RepairCategory.Dns,
            RequiresAdmin = true
        },
        new RepairAction
        {
            Id = RepairActionId.DnsGoogle,
            Category = RepairCategory.Dns,
            RequiresAdmin = true
        },
        new RepairAction
        {
            Id = RepairActionId.ResetStack,
            Category = RepairCategory.Advanced,
            RequiresAdmin = true
        },
        new RepairAction
        {
            Id = RepairActionId.FlushDns,
            Category = RepairCategory.Advanced,
            RequiresAdmin = false
        }
    ];

    public static IReadOnlyList<RepairAction> GetAvailableActions() => AvailableActions;

    public static async Task<RepairActionResult> ExecuteActionAsync(RepairActionId actionId, NetworkInfo? netInfo = null)
    {
        var action = AvailableActions.FirstOrDefault(a => a.Id == actionId);
        bool requiresAdmin = action?.RequiresAdmin ?? (actionId != RepairActionId.FlushDns && actionId != RepairActionId.PowerSaving);

        // If action requires administrator and current process is not admin, elevate cleanly via NetPulse internal action
        if (requiresAdmin && !AdminHelper.IsAdministrator())
        {
            return await AdminHelper.RunElevatedActionAsync(actionId, netInfo?.AdapterName);
        }

        return await ExecuteActionInternalAsync(actionId, netInfo);
    }

    public static Task<RepairActionResult> OptimizeAllAsync(NetworkInfo? netInfo = null) =>
        ExecuteActionAsync(RepairActionId.OptimizeAll, netInfo);

    /// <summary>
    /// Executes the repair action natively inside the application without any external script files.
    /// </summary>
    public static async Task<RepairActionResult> ExecuteActionInternalAsync(RepairActionId actionId, NetworkInfo? netInfo = null)
    {
        return actionId switch
        {
            RepairActionId.OptimizeAll => await OptimizeAllInternalAsync(netInfo),
            RepairActionId.TcpAutoTuning => await FixTcpAutoTuningAsync(),
            RepairActionId.NetworkThrottling => FixNetworkThrottlingDirect(),
            RepairActionId.PowerSaving => await FixPowerSavingAsync(),
            RepairActionId.DnsCloudflare => await SetDnsAsync(netInfo?.AdapterName ?? "Wi-Fi", "1.1.1.1", "1.0.0.1"),
            RepairActionId.DnsGoogle => await SetDnsAsync(netInfo?.AdapterName ?? "Wi-Fi", "8.8.8.8", "8.8.4.4"),
            RepairActionId.ResetStack => await FixResetStackAsync(),
            RepairActionId.FlushDns => await FlushDnsAsync(),
            _ => RepairActionResult.Fail(LocalizationService.Get("Repair_UnknownAction"))
        };
    }

    public static async Task<RepairActionResult> FixTcpAutoTuningAsync()
    {
        var p1 = await AdminHelper.RunCommandAsync("netsh.exe", "interface tcp set global autotuninglevel=normal", requireAdmin: true);
        var p2 = await AdminHelper.RunCommandAsync("netsh.exe", "interface tcp set global rss=enabled", requireAdmin: true);

        if (p1.Success && p2.Success)
        {
            return RepairActionResult.Ok(LocalizationService.Get("Repair_TcpSuccess"));
        }

        var errorMsg = !string.IsNullOrWhiteSpace(p1.Error) ? p1.Error : p2.Error;
        return RepairActionResult.Fail(LocalizationService.Get("Repair_TcpError", errorMsg));
    }

    public static RepairActionResult FixNetworkThrottlingDirect()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", writable: true);
            if (key != null)
            {
                key.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                key.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                return RepairActionResult.Ok(LocalizationService.Get("Repair_ThrottleSuccess"));
            }

            return RepairActionResult.Fail(LocalizationService.Get("Repair_ThrottleError", "Registry kaliti ochilmadi (Administrator huquqi zarur)."));
        }
        catch (Exception ex)
        {
            return RepairActionResult.Fail(LocalizationService.Get("Repair_ThrottleError", ex.Message));
        }
    }

    public static async Task<RepairActionResult> FixPowerSavingAsync()
    {
        try
        {
            const string wirelessSubgroup = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1";
            const string powerSavingSetting = "12bbebe6-58d6-4636-95bb-3217ef867c1a";
            var p1 = await AdminHelper.RunCommandAsync("powercfg.exe", $"/setacvalueindex SCHEME_CURRENT {wirelessSubgroup} {powerSavingSetting} 0");
            var p2 = await AdminHelper.RunCommandAsync("powercfg.exe", $"/setdcvalueindex SCHEME_CURRENT {wirelessSubgroup} {powerSavingSetting} 0");
            var p3 = await AdminHelper.RunCommandAsync("powercfg.exe", "/SetActive SCHEME_CURRENT");

            if (p1.Success && p2.Success && p3.Success)
            {
                return RepairActionResult.Ok(LocalizationService.Get("Repair_PowerSuccess"));
            }

            // Fallback: If non-elevated attempt failed and we are not admin, try running elevated
            if (!AdminHelper.IsAdministrator())
            {
                var elevatedResult = await AdminHelper.RunElevatedActionAsync(RepairActionId.PowerSaving);
                if (elevatedResult.Success)
                {
                    return RepairActionResult.Ok(LocalizationService.Get("Repair_PowerSuccess"));
                }
            }

            var errorMsg = !string.IsNullOrWhiteSpace(p1.Error) ? p1.Error : (!string.IsNullOrWhiteSpace(p2.Error) ? p2.Error : p3.Error);
            return RepairActionResult.Fail(LocalizationService.Get("Repair_PowerError", errorMsg));
        }
        catch (Exception ex)
        {
            return RepairActionResult.Fail(ex.Message);
        }
    }

    public static async Task<RepairActionResult> SetDnsAsync(string adapterName, string primaryDns, string secondaryDns)
    {
        try
        {
            if (!System.Net.IPAddress.TryParse(primaryDns, out _) || !System.Net.IPAddress.TryParse(secondaryDns, out _))
            {
                return RepairActionResult.Fail(LocalizationService.Get("Repair_DnsError", "Yaroqsiz IP manzil formati"));
            }

            bool success = false;
            string? lastError = null;

            // Method 1: Modern PowerShell cmdlet Set-DnsClientServerAddress (handles Unicode/Cyrillic adapter names perfectly)
            try
            {
                var safeAdapter = adapterName.Replace("'", "''");
                var psCommand = $"Set-DnsClientServerAddress -InterfaceAlias '{safeAdapter}' -ServerAddresses @('{primaryDns}','{secondaryDns}') -ErrorAction Stop";
                var psRes = AdminHelper.RunScriptInProcess(psCommand);
                if (psRes.Success)
                {
                    success = true;
                }
                else if (!string.IsNullOrWhiteSpace(psRes.Error))
                {
                    lastError = psRes.Error;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            // Method 2: WMI / Win32_NetworkAdapterConfiguration (In-process C#)
            if (!success)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT Index, Description, SettingID, IPEnabled FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        using var inParams = obj.GetMethodParameters("SetDNSServerSearchOrder");
                        inParams["DNSServerSearchOrder"] = new string[] { primaryDns, secondaryDns };
                        using var outParams = obj.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
                        var retVal = Convert.ToUInt32(outParams["ReturnValue"]);
                        if (retVal == 0 || retVal == 1) // 0 = Success, 1 = Success (Reboot required)
                        {
                            success = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastError ??= ex.Message;
                }
            }

            // Method 3: Fallback to netsh.exe
            if (!success)
            {
                var p1 = await AdminHelper.RunCommandAsync("netsh.exe", $"interface ipv4 set dns name=\"{adapterName}\" static {primaryDns} primary", requireAdmin: true);
                var p2 = await AdminHelper.RunCommandAsync("netsh.exe", $"interface ipv4 add dns name=\"{adapterName}\" {secondaryDns} index=2", requireAdmin: true);
                if (p1.Success)
                {
                    success = true;
                }
                else
                {
                    lastError = !string.IsNullOrWhiteSpace(p1.Error) ? p1.Error : p1.Output;
                }
            }

            await AdminHelper.RunCommandAsync("ipconfig.exe", "/flushdns");

            if (success)
            {
                return RepairActionResult.Ok(LocalizationService.Get("Repair_DnsSuccess", adapterName, primaryDns, secondaryDns));
            }

            return RepairActionResult.Fail(LocalizationService.Get("Repair_DnsError", lastError ?? "Noma'lum xatolik"));
        }
        catch (Exception ex)
        {
            return RepairActionResult.Fail(ex.Message);
        }
    }

    public static async Task<RepairActionResult> FixResetStackAsync()
    {
        try
        {
            var p1 = await AdminHelper.RunCommandAsync("netsh.exe", "winsock reset", requireAdmin: true);
            var resetLog = Path.Combine(Path.GetTempPath(), "netpulse_resetlog.txt");
            var p2 = await AdminHelper.RunCommandAsync("netsh.exe", $"int ip reset \"{resetLog}\"", requireAdmin: true);
            await AdminHelper.RunCommandAsync("ipconfig.exe", "/flushdns");

            if (p1.Success || p2.Success)
            {
                return RepairActionResult.Ok(LocalizationService.Get("Repair_ResetStackSuccess"), requiresRestart: true);
            }

            var errorMsg = !string.IsNullOrWhiteSpace(p1.Error) ? p1.Error : p1.Output;
            return RepairActionResult.Fail(LocalizationService.Get("Repair_ResetStackError", errorMsg));
        }
        catch (Exception ex)
        {
            return RepairActionResult.Fail(ex.Message);
        }
    }

    public static async Task<RepairActionResult> FlushDnsAsync()
    {
        var proc = await AdminHelper.RunCommandAsync("ipconfig.exe", "/flushdns");
        return proc.Success 
            ? RepairActionResult.Ok(LocalizationService.Get("Repair_FlushDnsSuccess")) 
            : RepairActionResult.Fail(LocalizationService.Get("Repair_FlushDnsError"));
    }

    private static async Task<RepairActionResult> OptimizeAllInternalAsync(NetworkInfo? netInfo)
    {
        var r1 = await FixTcpAutoTuningAsync();
        var r2 = FixNetworkThrottlingDirect();
        var r3 = await FixPowerSavingAsync();
        await FlushDnsAsync();

        var allSuccess = r1.Success && r2.Success && r3.Success;

        return allSuccess
            ? RepairActionResult.Ok(LocalizationService.Get("Repair_OptimizeAllSuccess"))
            : RepairActionResult.Ok(LocalizationService.Get("Repair_OptimizeAllPartial"));
    }
}
