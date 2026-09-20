using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class DriverCheck : INetworkCheck
{
    public DiagnosticCheckId Id => DiagnosticCheckId.Driver;
    public string Title => LocalizationService.Get("Check_Driver_Title");
    public string Description => LocalizationService.Get("Check_Driver_Desc");

    public Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        if (info.DriverVersion == null && !info.DriverDate.HasValue)
        {
            result.Status = DiagnosticStatus.Info;
            result.DetailsKey = "Check_Driver_Unknown_Details";
            result.DetailsArgs = new object[] { info.InterfaceDescription };
            result.Details = LocalizationService.Get("Check_Driver_Unknown_Details", info.InterfaceDescription);
            result.RecommendationKey = "Check_Driver_Unknown_Rec";
            result.Recommendation = LocalizationService.Get("Check_Driver_Unknown_Rec");
            return Task.FromResult(result);
        }

        var unknownText = LocalizationService.Get("Check_Driver_UnknownText");
        var versionStr = info.DriverVersion?.ToString() ?? unknownText;
        var dateStr = info.DriverDate.HasValue ? info.DriverDate.Value.ToString("yyyy-MM-dd") : unknownText;
        var detailsArgs = new object[] { versionStr, dateStr, info.InterfaceDescription };

        bool isOld = false;
        if (info.DriverDate.HasValue)
        {
            var driverDateTime = info.DriverDate.Value.ToDateTime(TimeOnly.MinValue);
            var ageYears = (DateTime.Now - driverDateTime).TotalDays / 365.25;
            if (ageYears > 3.0)
            {
                isOld = true;
            }
        }

        if (isOld)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_Driver_Details_Old";
            result.DetailsArgs = detailsArgs;
            result.Details = LocalizationService.Get("Check_Driver_Details_Old", detailsArgs);
            result.RecommendationKey = "Check_Driver_Old_Rec";
            result.Recommendation = LocalizationService.Get("Check_Driver_Old_Rec");
        }
        else
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_Driver_Details";
            result.DetailsArgs = detailsArgs;
            result.Details = LocalizationService.Get("Check_Driver_Details", detailsArgs);
            result.RecommendationKey = "Check_Driver_Optimal_Rec";
            result.Recommendation = LocalizationService.Get("Check_Driver_Optimal_Rec");
        }

        return Task.FromResult(result);
    }
}
