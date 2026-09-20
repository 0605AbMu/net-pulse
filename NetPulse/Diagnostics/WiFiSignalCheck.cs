using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class WiFiSignalCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.WiFiSignal;
    public string Title => LocalizationService.Get("Check_WiFiSignal_Title");
    public string Description => LocalizationService.Get("Check_WiFiSignal_Desc");

    public Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
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
            result.DetailsKey = "Check_WiFiSignal_NotConnected_Details";
            result.Details = LocalizationService.Get("Check_WiFiSignal_NotConnected_Details");
            result.RecommendationKey = "Check_WiFiSignal_NotConnected_Rec";
            result.Recommendation = LocalizationService.Get("Check_WiFiSignal_NotConnected_Rec");
            return Task.FromResult(result);
        }

        if (info.ConnectionType != NetworkConnectionType.WiFi)
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_WiFiSignal_Ethernet_Details";
            result.DetailsArgs = new object[] { info.ConnectionTypeDisplay, info.ReceiveRateMbps };
            result.Details = LocalizationService.Get("Check_WiFiSignal_Ethernet_Details", info.ConnectionTypeDisplay, info.ReceiveRateMbps);
            result.RecommendationKey = "Check_WiFiSignal_Ethernet_Rec";
            result.Recommendation = LocalizationService.Get("Check_WiFiSignal_Ethernet_Rec");
            return Task.FromResult(result);
        }

        var detailsArgs = new object[] { info.Ssid, info.SignalPercentage, info.BandDisplay, info.ReceiveRateMbps };

        if (info.SignalPercentage < 40)
        {
            result.Status = DiagnosticStatus.Danger;
            result.DetailsKey = "Check_WiFiSignal_Details_Weak";
            result.DetailsArgs = detailsArgs;
            result.Details = LocalizationService.Get("Check_WiFiSignal_Details_Weak", detailsArgs);
            result.RecommendationKey = "Check_WiFiSignal_Weak_Rec";
            result.Recommendation = LocalizationService.Get("Check_WiFiSignal_Weak_Rec");
        }
        else if (info.SignalPercentage < 65)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_WiFiSignal_Details_Fair";
            result.DetailsArgs = detailsArgs;
            result.Details = LocalizationService.Get("Check_WiFiSignal_Details_Fair", detailsArgs);
            result.RecommendationKey = "Check_WiFiSignal_Fair_Rec";
            result.Recommendation = LocalizationService.Get("Check_WiFiSignal_Fair_Rec");
        }
        else if (info.Band == WiFiFrequencyBand.Band2_4GHz && info.ReceiveRateMbps < 72)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_WiFiSignal_Details_Legacy24";
            result.DetailsArgs = detailsArgs;
            result.Details = LocalizationService.Get("Check_WiFiSignal_Details_Legacy24", detailsArgs);
            result.RecommendationKey = "Check_WiFiSignal_Legacy24_Rec";
            result.Recommendation = LocalizationService.Get("Check_WiFiSignal_Legacy24_Rec");
        }
        else
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_WiFiSignal_Details";
            result.DetailsArgs = detailsArgs;
            result.Details = LocalizationService.Get("Check_WiFiSignal_Details", detailsArgs);
            result.RecommendationKey = "Check_WiFiSignal_Good_Rec";
            result.Recommendation = LocalizationService.Get("Check_WiFiSignal_Good_Rec");
        }

        return Task.FromResult(result);
    }
}
