using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class TcpAutoTuningCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.TcpAutoTuning;
    public string Title => LocalizationService.Get("Check_TcpAutoTuning_Title");
    public string Description => LocalizationService.Get("Check_TcpAutoTuning_Desc");

    public async Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        var level = await NetworkHelper.GetTcpAutoTuningLevelAsync();

        if (level == TcpAutoTuningLevel.Disabled)
        {
            result.Status = DiagnosticStatus.Danger;
            result.DetailsKey = "Check_TcpAutoTuning_Disabled_Details";
            result.DetailsArgs = new object[] { level };
            result.Details = LocalizationService.Get("Check_TcpAutoTuning_Disabled_Details", level);
            result.RecommendationKey = "Check_TcpAutoTuning_Disabled_Rec";
            result.Recommendation = LocalizationService.Get("Check_TcpAutoTuning_Disabled_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.TcpAutoTuning;
        }
        else if (level != TcpAutoTuningLevel.Normal && level != TcpAutoTuningLevel.Experimental)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_TcpAutoTuning_NonStandard_Details";
            result.DetailsArgs = new object[] { level };
            result.Details = LocalizationService.Get("Check_TcpAutoTuning_NonStandard_Details", level);
            result.RecommendationKey = "Check_TcpAutoTuning_NonStandard_Rec";
            result.Recommendation = LocalizationService.Get("Check_TcpAutoTuning_NonStandard_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.TcpAutoTuning;
        }
        else
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_TcpAutoTuning_Optimal_Details";
            result.DetailsArgs = new object[] { level };
            result.Details = LocalizationService.Get("Check_TcpAutoTuning_Optimal_Details", level);
            result.RecommendationKey = "Check_TcpAutoTuning_Optimal_Rec";
            result.Recommendation = LocalizationService.Get("Check_TcpAutoTuning_Optimal_Rec");
        }

        return result;
    }
}
