namespace NetPulse.Models;

public enum DiagnosticCheckId
{
    WiFiSignal,
    PowerManagement,
    TcpAutoTuning,
    NetworkThrottling,
    Dns,
    WinsockStack,
    ChannelInterference,
    Driver,
    SpeedTest
}

public enum RepairActionId
{
    OptimizeAll,
    TcpAutoTuning,
    NetworkThrottling,
    PowerSaving,
    DnsCloudflare,
    DnsGoogle,
    ResetStack,
    FlushDns
}

public enum RepairCategory
{
    Recommended,
    Dns,
    Advanced
}

public enum NetworkConnectionType
{
    Unknown,
    WiFi,
    Ethernet
}

public enum WiFiFrequencyBand
{
    Unknown,
    Band2_4GHz,
    Band5GHz,
    Band6GHz
}

public enum WiFiRadioStandard
{
    Unknown,
    Legacy80211b,
    Legacy80211g,
    WiFi4_80211n,
    WiFi5_80211ac,
    WiFi6_80211ax,
    WiFi7_80211be
}

public enum TcpAutoTuningLevel
{
    Unknown,
    Normal,
    Disabled,
    HighlyRestricted,
    Restricted,
    Experimental
}

public enum DiagnosticFilter
{
    All,
    Issues,
    Success
}
