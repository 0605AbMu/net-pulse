using System.Net;
using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;

namespace NetPulse.Diagnostics;

public class DnsCheck : INetworkCheck
{
    private static readonly IPAddress CloudflarePrimary = IPAddress.Parse("1.1.1.1");
    private static readonly IPAddress CloudflareSecondary = IPAddress.Parse("1.0.0.1");
    private static readonly IPAddress GooglePrimary = IPAddress.Parse("8.8.8.8");
    private static readonly IPAddress GoogleSecondary = IPAddress.Parse("8.8.4.4");

    public DiagnosticCheckId Id => DiagnosticCheckId.Dns;
    public string Title => LocalizationService.Get("Check_Dns_Title");
    public string Description => LocalizationService.Get("Check_Dns_Desc");

    public async Task<DiagnosticResult> RunCheckAsync(NetworkInfo info)
    {
        var result = new DiagnosticResult
        {
            CheckId = Id,
            Title = Title,
            Description = Description
        };

        var dnsList = info.DnsServers.Length > 0 
            ? string.Join(", ", info.DnsServers.Select(d => d.ToString())) 
            : LocalizationService.Get("Check_Dns_Dhcp");

        var dnsRes = await NetworkHelper.CheckDnsResolutionAsync("google.com");

        if (!dnsRes.Success)
        {
            result.Status = DiagnosticStatus.Danger;
            result.DetailsKey = "Check_Dns_Failed_Details";
            result.DetailsArgs = new object[] { dnsList };
            result.Details = LocalizationService.Get("Check_Dns_Failed_Details", dnsList);
            result.RecommendationKey = "Check_Dns_Failed_Rec";
            result.Recommendation = LocalizationService.Get("Check_Dns_Failed_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.DnsCloudflare;
            return result;
        }

        var isCloudflare = info.DnsServers.Any(d => d.Equals(CloudflarePrimary) || d.Equals(CloudflareSecondary));
        var isGoogle = info.DnsServers.Any(d => d.Equals(GooglePrimary) || d.Equals(GoogleSecondary));
        var isFastPublicDns = isCloudflare || isGoogle;

        // Agar foydalanuvchi allaqachon tezkor ommaviy DNS ga ulangan bo'lsa:
        if (isFastPublicDns)
        {
            result.Status = DiagnosticStatus.Success;
            var providerName = isCloudflare ? "Cloudflare DNS (1.1.1.1)" : "Google DNS (8.8.8.8)";
            var altProvider = isCloudflare ? "Google DNS (8.8.8.8)" : "Cloudflare DNS (1.1.1.1)";
            result.DetailsKey = "Check_Dns_Fast_Details";
            result.DetailsArgs = new object[] { providerName, dnsList, dnsRes.LatencyMs };
            result.Details = LocalizationService.Get("Check_Dns_Fast_Details", providerName, dnsList, dnsRes.LatencyMs);
            result.RecommendationKey = "Check_Dns_Fast_Rec";
            result.RecommendationArgs = new object[] { providerName, altProvider };
            result.Recommendation = LocalizationService.Get("Check_Dns_Fast_Rec", providerName, altProvider);
            result.CanAutoRepair = false;
            return result;
        }

        // Provayder DNS kechikishi yuqori bo'lsa (> 200 ms):
        if (dnsRes.LatencyMs > 200)
        {
            result.Status = DiagnosticStatus.Warning;
            result.DetailsKey = "Check_Dns_HighLatency_Details";
            result.DetailsArgs = new object[] { dnsRes.LatencyMs, dnsList };
            result.Details = LocalizationService.Get("Check_Dns_HighLatency_Details", dnsRes.LatencyMs, dnsList);
            result.RecommendationKey = "Check_Dns_HighLatency_Rec";
            result.Recommendation = LocalizationService.Get("Check_Dns_HighLatency_Rec");
            result.CanAutoRepair = true;
            result.RepairActionId = RepairActionId.DnsCloudflare;
        }
        else
        {
            result.Status = DiagnosticStatus.Success;
            result.DetailsKey = "Check_Dns_Normal_Details";
            result.DetailsArgs = new object[] { dnsRes.LatencyMs, dnsList };
            result.Details = LocalizationService.Get("Check_Dns_Normal_Details", dnsRes.LatencyMs, dnsList);
            result.RecommendationKey = "Check_Dns_Normal_Rec";
            result.Recommendation = LocalizationService.Get("Check_Dns_Normal_Rec");
            result.CanAutoRepair = false;
        }

        return result;
    }
}
