using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class NetworkThrottlingCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.NetworkThrottling;
    public string Title => LocalizationService.Get("Check_NetworkThrottling_Title");
    public string Description => LocalizationService.Get("Check_NetworkThrottling_Desc");

    public Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        var throttlingIndex = NetworkHelper.GetNetworkThrottlingIndex();

        // Default Windows is 10 (0x0000000a), which throttles non-multimedia traffic
        // Disabled is 0xFFFFFFFF (-1 or 4294967295)
        if (throttlingIndex.HasValue)
        {
            if (throttlingIndex.Value == -1 || (uint)throttlingIndex.Value == 0xFFFFFFFF)
            {
                result.Status = DiagnosticStatus.Success;
                result.DetailsKey = "Check_NetworkThrottling_Disabled_Details";
                result.Details = LocalizationService.Get("Check_NetworkThrottling_Disabled_Details");
                result.RecommendationKey = "Check_NetworkThrottling_Disabled_Rec";
                result.Recommendation = LocalizationService.Get("Check_NetworkThrottling_Disabled_Rec");
            }
            else
            {
                result.Status = DiagnosticStatus.Warning;
                result.DetailsKey = "Check_NetworkThrottling_Active_Details";
                result.DetailsArgs = new object[] { throttlingIndex.Value };
                result.Details = LocalizationService.Get("Check_NetworkThrottling_Active_Details", throttlingIndex.Value);
                result.RecommendationKey = "Check_NetworkThrottling_Active_Rec";
                result.Recommendation = LocalizationService.Get("Check_NetworkThrottling_Active_Rec");
                result.CanAutoRepair = true;
                result.RepairActionId = RepairActionId.NetworkThrottling;
            }
        }
        else
        {
            result.Status = DiagnosticStatus.Info;
            result.DetailsKey = "Check_NetworkThrottling_Missing_Details";
            result.Details = LocalizationService.Get("Check_NetworkThrottling_Missing_Details");
            result.RecommendationKey = "Check_NetworkThrottling_Missing_Rec";
            result.Recommendation = LocalizationService.Get("Check_NetworkThrottling_Missing_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.NetworkThrottling;
        }

        return Task.FromResult(result);
    }
}
