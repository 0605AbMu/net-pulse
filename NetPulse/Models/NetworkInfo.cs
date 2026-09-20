using System.Net;

namespace NetPulse.Models;

public class NetworkInfo
{
    public string AdapterName { get; set; } = "Noma'lum";
    public string InterfaceDescription { get; set; } = string.Empty;
    public NetworkConnectionType ConnectionType { get; set; } = NetworkConnectionType.Unknown;
    public bool IsConnected { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public MacAddress? Bssid { get; set; }
    public int SignalPercentage { get; set; }
    public WiFiRadioStandard RadioType { get; set; } = WiFiRadioStandard.Unknown;
    public WiFiFrequencyBand Band { get; set; } = WiFiFrequencyBand.Unknown;
    public int Channel { get; set; }
    public double ReceiveRateMbps { get; set; }
    public double TransmitRateMbps { get; set; }
    public IPAddress? IpAddress { get; set; }
    public IPAddress? DefaultGateway { get; set; }
    public IPAddress[] DnsServers { get; set; } = Array.Empty<IPAddress>();
    public Version? DriverVersion { get; set; }
    public DateOnly? DriverDate { get; set; }
    public MacAddress? MacAddress { get; set; }

    // Helper display properties for UI
    public string ConnectionTypeDisplay => ConnectionType switch
    {
        NetworkConnectionType.WiFi => "Wi-Fi",
        NetworkConnectionType.Ethernet => "Ethernet",
        _ => "Noma'lum"
    };

    public string BandDisplay => Band switch
    {
        WiFiFrequencyBand.Band2_4GHz => "2.4 GHz",
        WiFiFrequencyBand.Band5GHz => "5 GHz",
        WiFiFrequencyBand.Band6GHz => "6 GHz",
        _ => "Noma'lum"
    };

    public string RadioTypeDisplay => RadioType switch
    {
        WiFiRadioStandard.WiFi4_80211n => "802.11n (Wi-Fi 4)",
        WiFiRadioStandard.WiFi5_80211ac => "802.11ac (Wi-Fi 5)",
        WiFiRadioStandard.WiFi6_80211ax => "802.11ax (Wi-Fi 6)",
        WiFiRadioStandard.WiFi7_80211be => "802.11be (Wi-Fi 7)",
        WiFiRadioStandard.Legacy80211b => "802.11b",
        WiFiRadioStandard.Legacy80211g => "802.11g",
        _ => "Noma'lum"
    };
}
