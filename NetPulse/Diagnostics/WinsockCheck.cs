using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class WinsockCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.WinsockStack;
    public string Title => LocalizationService.Get("Check_WinsockStack_Title");
    public string Description => LocalizationService.Get("Check_WinsockStack_Desc");

    public async Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        if (info.DefaultGateway == null)
        {
            result.Status = DiagnosticStatus.Danger;
            result.DetailsKey = "Check_Winsock_NoGateway_Details";
            result.Details = LocalizationService.Get("Check_Winsock_NoGateway_Details");
            result.RecommendationKey = "Check_Winsock_NoGateway_Rec";
            result.Recommendation = LocalizationService.Get("Check_Winsock_NoGateway_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.ResetStack;
            return result;
        }

        var pingResult = await NetworkHelper.PingAsync(info.DefaultGateway.ToString(), count: 6, timeoutMs: 1000);

        if (pingResult.PacketLossPercent > 0 || pingResult.AvgLatencyMs > 25)
        {
            result.Status = pingResult.PacketLossPercent > 20 ? DiagnosticStatus.Danger : DiagnosticStatus.Warning;
            result.DetailsKey = "Check_Winsock_Unstable_Details";
            result.DetailsArgs = new object[] { pingResult.AvgLatencyMs, pingResult.PacketLossPercent, info.DefaultGateway };
            result.Details = LocalizationService.Get("Check_Winsock_Unstable_Details", pingResult.AvgLatencyMs, pingResult.PacketLossPercent, info.DefaultGateway);
            result.RecommendationKey = "Check_Winsock_Unstable_Rec";
            result.Recommendation = LocalizationService.Get("Check_Winsock_Unstable_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.ResetStack;
        }
        else if (pingResult.AvgLatencyMs < 0)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_Winsock_NoPing_Details";
            result.DetailsArgs = new object[] { info.DefaultGateway };
            result.Details = LocalizationService.Get("Check_Winsock_NoPing_Details", info.DefaultGateway);
            result.RecommendationKey = "Check_Winsock_NoPing_Rec";
            result.Recommendation = LocalizationService.Get("Check_Winsock_NoPing_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.ResetStack;
        }
        else
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_Winsock_Optimal_Details";
            result.DetailsArgs = new object[] { pingResult.AvgLatencyMs, info.DefaultGateway };
            result.Details = LocalizationService.Get("Check_Winsock_Optimal_Details", pingResult.AvgLatencyMs, info.DefaultGateway);
            result.RecommendationKey = "Check_Winsock_Optimal_Rec";
            result.Recommendation = LocalizationService.Get("Check_Winsock_Optimal_Rec");
        }

        return result;
    }
}
