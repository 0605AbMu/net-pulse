using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class ChannelInterferenceCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.ChannelInterference;
    public string Title => LocalizationService.Get("Check_ChannelInterference_Title");
    public string Description => LocalizationService.Get("Check_ChannelInterference_Desc");

    public async Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        if (info.ConnectionType != NetworkConnectionType.WiFi || info.Channel <= 0)
        {
            result.Status = DiagnosticStatus.Info;
            result.DetailsKey = "Check_ChannelInterference_NA_Details";
            result.Details = LocalizationService.Get("Check_ChannelInterference_NA_Details");
            result.RecommendationKey = "Check_ChannelInterference_NA_Rec";
            result.Recommendation = LocalizationService.Get("Check_ChannelInterference_NA_Rec");
            return result;
        }

        var neighborCount = await NetworkHelper.GetNearbyNetworksCountOnSameChannelAsync(info.Channel);

        var bandInfo = info.BandDisplay;

        if (neighborCount >= 4)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_ChannelInterference_High_Details";
            result.DetailsArgs = new object[] { info.Channel, bandInfo, neighborCount };
            result.Details = LocalizationService.Get("Check_ChannelInterference_High_Details", info.Channel, bandInfo, neighborCount);
            var recKey = info.Band == WiFiFrequencyBand.Band2_4GHz
                ? "Check_ChannelInterference_High_Rec_24"
                : "Check_ChannelInterference_High_Rec_5";
            result.RecommendationKey = recKey;
            result.Recommendation = LocalizationService.Get(recKey);
        }
        else if (neighborCount > 0)
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_ChannelInterference_Low_Details";
            result.DetailsArgs = new object[] { info.Channel, bandInfo, neighborCount };
            result.Details = LocalizationService.Get("Check_ChannelInterference_Low_Details", info.Channel, bandInfo, neighborCount);
            result.RecommendationKey = "Check_ChannelInterference_Low_Rec";
            result.Recommendation = LocalizationService.Get("Check_ChannelInterference_Low_Rec");
        }
        else
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_ChannelInterference_Clean_Details";
            result.DetailsArgs = new object[] { info.Channel, bandInfo };
            result.Details = LocalizationService.Get("Check_ChannelInterference_Clean_Details", info.Channel, bandInfo);
            result.RecommendationKey = "Check_ChannelInterference_Clean_Rec";
            result.Recommendation = LocalizationService.Get("Check_ChannelInterference_Clean_Rec");
        }

        return result;
    }
}
