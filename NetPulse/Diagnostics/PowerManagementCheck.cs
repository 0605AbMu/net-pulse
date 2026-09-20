using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class PowerManagementCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.PowerManagement;
    public string Title => LocalizationService.Get("Check_PowerManagement_Title");
    public string Description => LocalizationService.Get("Check_PowerManagement_Desc");

    public async Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        if (info.ConnectionType != NetworkConnectionType.WiFi)
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_PowerManagement_Ethernet_Details";
            result.Details = LocalizationService.Get("Check_PowerManagement_Ethernet_Details");
            result.RecommendationKey = "Check_PowerManagement_Ethernet_Rec";
            result.Recommendation = LocalizationService.Get("Check_PowerManagement_Ethernet_Rec");
            return result;
        }

        // Query powercfg wireless power saving mode (Official Windows Wireless Adapter Settings GUIDs)
        const string wirelessSubgroup = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1";
        const string powerSavingSetting = "12bbebe6-58d6-4636-95bb-3217ef867c1a";
        var procResult = await AdminHelper.RunCommandAsync("powercfg.exe", $"/query SCHEME_CURRENT {wirelessSubgroup} {powerSavingSetting}");

        bool isPowerSavingActive = false;
        if (procResult.Success)
        {
            // If current AC or DC index is > 0 (1 = Low Power Saving, 2 = Medium, 3 = Maximum Power Saving)
            // 0 = Maximum Performance (Optimal)
            var stdout = procResult.Output;
            if (stdout.Contains("Current AC Power Setting Index: 0x00000001") ||
                stdout.Contains("Current AC Power Setting Index: 0x00000002") ||
                stdout.Contains("Current AC Power Setting Index: 0x00000003") ||
                stdout.Contains("Current DC Power Setting Index: 0x00000001") ||
                stdout.Contains("Current DC Power Setting Index: 0x00000002") ||
                stdout.Contains("Current DC Power Setting Index: 0x00000003"))
            {
                isPowerSavingActive = true;
            }
        }

        if (isPowerSavingActive)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_PowerManagement_Active_Details";
            result.Details = LocalizationService.Get("Check_PowerManagement_Active_Details");
            result.RecommendationKey = "Check_PowerManagement_Active_Rec";
            result.Recommendation = LocalizationService.Get("Check_PowerManagement_Active_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.PowerSaving;
        }
        else
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_PowerManagement_Optimal_Details";
            result.Details = LocalizationService.Get("Check_PowerManagement_Optimal_Details");
            result.RecommendationKey = "Check_PowerManagement_Optimal_Rec";
            result.Recommendation = LocalizationService.Get("Check_PowerManagement_Optimal_Rec");
        }

        return result;
    }
}
