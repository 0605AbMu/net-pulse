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
            RequiresAdmin = true
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
        return actionId switch
        {
            RepairActionId.OptimizeAll => await OptimizeAllAsync(netInfo),
            RepairActionId.TcpAutoTuning => await FixTcpAutoTuningAsync(),
            RepairActionId.NetworkThrottling => await FixNetworkThrottlingAsync(),
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
        var script = @"
netsh interface tcp set global autotuninglevel=normal
netsh interface tcp set global rss=enabled
";
        var proc = await AdminHelper.RunElevatedScriptAsync(script);
        if (proc.Success)
        {
            return RepairActionResult.Ok(LocalizationService.Get("Repair_TcpSuccess"));
        }

        var errorMsg = !string.IsNullOrWhiteSpace(proc.Error) ? proc.Error : proc.Output;
        return RepairActionResult.Fail(LocalizationService.Get("Repair_TcpError", errorMsg));
    }

    public static async Task<RepairActionResult> FixNetworkThrottlingAsync()
    {
        try
        {
            var psScript = @"
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' -Name 'NetworkThrottlingIndex' -Value 4294967295 -Type DWord -Force
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' -Name 'SystemResponsiveness' -Value 0 -Type DWord -Force
";
            var proc = await AdminHelper.RunElevatedScriptAsync(psScript);

            if (proc.Success)
            {
                return RepairActionResult.Ok(LocalizationService.Get("Repair_ThrottleSuccess"));
            }

            var errorMsg = !string.IsNullOrWhiteSpace(proc.Error) ? proc.Error : proc.Output;
            return RepairActionResult.Fail(LocalizationService.Get("Repair_ThrottleError", errorMsg));
        }
        catch (Exception ex)
        {
            return RepairActionResult.Fail(ex.Message);
        }
    }

    public static async Task<RepairActionResult> FixPowerSavingAsync()
    {
        try
        {
            var script = @"
powercfg /setacvalueindex SCHEME_CURRENT 19cbb8fa-5279-450e-9f80-4a60194f514b 12bbe462-763e-4327-a180-a69144c93be8 0
powercfg /setdcvalueindex SCHEME_CURRENT 19cbb8fa-5279-450e-9f80-4a60194f514b 12bbe462-763e-4327-a180-a69144c93be8 0
powercfg /SetActive SCHEME_CURRENT
";
            var proc = await AdminHelper.RunElevatedScriptAsync(script);

            if (proc.Success)
            {
                return RepairActionResult.Ok(LocalizationService.Get("Repair_PowerSuccess"));
            }

            var errorMsg = !string.IsNullOrWhiteSpace(proc.Error) ? proc.Error : proc.Output;
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
            var psScript = $@"
Set-DnsClientServerAddress -InterfaceAlias '{adapterName}' -ServerAddresses ('{primaryDns}','{secondaryDns}') -ErrorAction SilentlyContinue
Clear-DnsClientCache
";
            var proc = await AdminHelper.RunElevatedScriptAsync(psScript);

            if (proc.Success)
            {
                return RepairActionResult.Ok(LocalizationService.Get("Repair_DnsSuccess", adapterName, primaryDns, secondaryDns));
            }

            var errorMsg = !string.IsNullOrWhiteSpace(proc.Error) ? proc.Error : proc.Output;
            return RepairActionResult.Fail(LocalizationService.Get("Repair_DnsError", errorMsg));
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
            var script = @"
netsh winsock reset
netsh int ip reset
ipconfig /flushdns
";
            var proc = await AdminHelper.RunElevatedScriptAsync(script);

            if (proc.Success)
            {
                return RepairActionResult.Ok(LocalizationService.Get("Repair_ResetStackSuccess"), requiresRestart: true);
            }

            var errorMsg = !string.IsNullOrWhiteSpace(proc.Error) ? proc.Error : proc.Output;
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

    public static async Task<RepairActionResult> OptimizeAllAsync(NetworkInfo? netInfo)
    {
        // Execute all optimizations in a SINGLE script so Windows asks for UAC elevation only ONCE.
        var script = @"
$log = @()

# 1. TCP Window Auto-Tuning & Offload
try {
    netsh interface tcp set global autotuninglevel=normal | Out-Null
    netsh interface tcp set global rss=enabled | Out-Null
    $log += 'TCP:OK'
} catch {
    $log += 'TCP:FAIL'
}

# 2. Network Throttling & System Responsiveness
try {
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' -Name 'NetworkThrottlingIndex' -Value 4294967295 -Type DWord -Force
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' -Name 'SystemResponsiveness' -Value 0 -Type DWord -Force
    $log += 'THROTTLE:OK'
} catch {
    $log += 'THROTTLE:FAIL'
}

# 3. Wi-Fi Power Saving (Maximum Performance)
try {
    powercfg /setacvalueindex SCHEME_CURRENT 19cbb8fa-5279-450e-9f80-4a60194f514b 12bbe462-763e-4327-a180-a69144c93be8 0 | Out-Null
    powercfg /setdcvalueindex SCHEME_CURRENT 19cbb8fa-5279-450e-9f80-4a60194f514b 12bbe462-763e-4327-a180-a69144c93be8 0 | Out-Null
    powercfg /SetActive SCHEME_CURRENT | Out-Null
    $log += 'POWER:OK'
} catch {
    $log += 'POWER:FAIL'
}

# 4. Flush DNS
try {
    ipconfig /flushdns | Out-Null
    $log += 'DNS:OK'
} catch {
    $log += 'DNS:FAIL'
}

Write-Output ($log -join ';')
";

        var proc = await AdminHelper.RunElevatedScriptAsync(script);

        if (!proc.Success && string.IsNullOrWhiteSpace(proc.Output))
        {
            return RepairActionResult.Fail(LocalizationService.Get("Repair_AdminRequired"));
        }

        var results = proc.Output.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
        var tcpOk = results.Any(r => r.Trim() == "TCP:OK");
        var throttleOk = results.Any(r => r.Trim() == "THROTTLE:OK");
        var powerOk = results.Any(r => r.Trim() == "POWER:OK");

        var allSuccess = tcpOk && throttleOk && powerOk;

        return allSuccess
            ? RepairActionResult.Ok(LocalizationService.Get("Repair_OptimizeAllSuccess"))
            : RepairActionResult.Ok(LocalizationService.Get("Repair_OptimizeAllPartial"));
    }
}
