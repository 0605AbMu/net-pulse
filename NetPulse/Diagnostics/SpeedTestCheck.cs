using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class SpeedTestCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.SpeedTest;
    public string Title => LocalizationService.Get("Check_SpeedTest_Title");
    public string Description => LocalizationService.Get("Check_SpeedTest_Desc");

    public async Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        if (!info.IsConnected)
        {
            result.Status = DiagnosticStatus.Danger;
            result.DetailsKey = "Check_SpeedTest_NoConn_Details";
            result.Details = LocalizationService.Get("Check_SpeedTest_NoConn_Details");
            result.RecommendationKey = "Check_SpeedTest_NoConn_Rec";
            result.Recommendation = LocalizationService.Get("Check_SpeedTest_NoConn_Rec");
            return result;
        }

        try
        {
            var speedMbps = await NetworkHelper.TestDownloadSpeedMbpsAsync(defaultGateway: info.DefaultGateway);

            var linkSpeed = Math.Max(info.ReceiveRateMbps, info.TransmitRateMbps);
            var linkText = linkSpeed > 0 ? $"{linkSpeed} Mbps" : LocalizationService.Get("Check_SpeedTest_Unknown");

            if (speedMbps <= 0)
            {
                result.Status = DiagnosticStatus.Warning;
                result.DetailsKey = "Check_SpeedTest_Failed_Details";
                result.Details = LocalizationService.Get("Check_SpeedTest_Failed_Details");
                result.RecommendationKey = "Check_SpeedTest_Failed_Rec";
                result.Recommendation = LocalizationService.Get("Check_SpeedTest_Failed_Rec");
                return result;
            }

            var speedFormatted = $"{speedMbps:F1}";
            var isSlow = linkSpeed >= 50 && speedMbps < 10;
            var isMedium = !isSlow && speedMbps < 25;

            var detailsKey = isSlow ? "Check_SpeedTest_Details_Slow" : "Check_SpeedTest_Details";
            var detailsArgs = new object[] { speedFormatted, linkText };

            result.DetailsKey = detailsKey;
            result.DetailsArgs = detailsArgs;
            result.Details = LocalizationService.Get(detailsKey, detailsArgs);

            // The user's exact symptom: Link is high (e.g. 100Mbps or 866Mbps), but real speed is only 5-6 Mbps!
            if (isSlow)
            {
                result.Status = DiagnosticStatus.Danger;
                result.RecommendationKey = "Check_SpeedTest_Rec_Slow";
                result.RecommendationArgs = new object[] { linkText, speedFormatted };
                result.Recommendation = LocalizationService.Get("Check_SpeedTest_Rec_Slow", linkText, speedFormatted);
                result.CanAutoRepair = true;
                result.RepairActionId = RepairActionId.OptimizeAll;
            }
            else if (isMedium)
            {
                result.Status = DiagnosticStatus.Warning;
                result.RecommendationKey = "Check_SpeedTest_Rec_Medium";
                result.Recommendation = LocalizationService.Get("Check_SpeedTest_Rec_Medium");
                result.CanAutoRepair = true;
                result.RepairActionId = RepairActionId.OptimizeAll;
            }
            else
            {
                result.Status = DiagnosticStatus.Success;
                result.RecommendationKey = "Check_SpeedTest_Rec_Good";
                result.Recommendation = LocalizationService.Get("Check_SpeedTest_Rec_Good");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_SpeedTest_Error_Details";
            result.DetailsArgs = new object[] { ex.Message };
            result.Details = LocalizationService.Get("Check_SpeedTest_Error_Details", ex.Message);
            result.RecommendationKey = "Check_SpeedTest_Error_Rec";
            result.Recommendation = LocalizationService.Get("Check_SpeedTest_Error_Rec");
            return result;
        }
    }
}
